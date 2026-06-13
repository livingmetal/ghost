using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using LivingMetalGhost.Core.Config;
using LivingMetalGhost.Core.Models;

namespace LivingMetalGhost.Providers.Llm;

public sealed class CodexCliProvider : ILlmProvider
{
    private readonly AppConfigLoader _configLoader;

    public CodexCliProvider(AppConfigLoader configLoader)
    {
        _configLoader = configLoader;
    }

    public string Name => "Codex";

    public async Task<LlmResponse> GenerateAsync(LlmRequest request, CancellationToken ct)
    {
        var config = _configLoader.Load();
        var executable = string.IsNullOrWhiteSpace(config.Llm.CodexExecutable)
            ? "codex"
            : Environment.ExpandEnvironmentVariables(config.Llm.CodexExecutable.Trim());
        var workingDirectory = ResolveWorkingDirectory(config.Llm.CodexWorkingDirectory);
        var outputDirectory = Path.Combine(Path.GetTempPath(), "LivingMetalGhost", "codex");
        Directory.CreateDirectory(outputDirectory);
        var outputFile = Path.Combine(outputDirectory, $"last-message-{Guid.NewGuid():N}.txt");

        var arguments = new List<string>
        {
            "exec",
            "--sandbox",
            "read-only",
            "--skip-git-repo-check",
            "--json",
            "--output-last-message",
            outputFile,
            "-C",
            workingDirectory
        };

        if (!string.IsNullOrWhiteSpace(request.Model) &&
            !string.Equals(request.Model, "default", StringComparison.OrdinalIgnoreCase))
        {
            arguments.Add("-m");
            arguments.Add(request.Model.Trim());
        }

        using var process = new Process
        {
            StartInfo = CreateStartInfo(executable, arguments, workingDirectory)
        };

        try
        {
            process.Start();
        }
        catch (Win32Exception ex)
        {
            throw new InvalidOperationException(
                "Codex CLI를 실행하지 못했습니다. 설정에서 독립 Codex CLI 실행 경로를 지정하고 로그인을 완료해 주세요.",
                ex);
        }

        using var cancellationRegistration = ct.Register(() =>
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch
            {
            }
        });

        var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
        var stderrTask = process.StandardError.ReadToEndAsync(ct);
        await process.StandardInput.WriteAsync(BuildPrompt(request));
        process.StandardInput.Close();

        var timeoutSeconds = Math.Clamp(config.Llm.CodexTimeoutSeconds, 30, 900);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            TryKill(process);
            throw new TimeoutException($"Codex 응답이 {timeoutSeconds}초 안에 완료되지 않았습니다.");
        }

        var stdout = await stdoutTask;
        var stderr = await stderrTask;
        var text = File.Exists(outputFile)
            ? (await File.ReadAllTextAsync(outputFile, ct)).Trim()
            : string.Empty;
        TryDelete(outputFile);

        if (process.ExitCode != 0)
        {
            var detail = string.IsNullOrWhiteSpace(stderr) ? stdout : stderr;
            detail = detail.Length > 800 ? detail[..800] : detail;
            throw new InvalidOperationException(
                $"Codex 실행이 실패했습니다. 종료 코드 {process.ExitCode}: {detail.Trim()}");
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidOperationException("Codex가 최종 응답을 반환하지 않았습니다.");
        }

        return new LlmResponse
        {
            Text = text,
            FromFallback = false
        };
    }

    public async IAsyncEnumerable<LlmStreamChunk> StreamAsync(
        LlmRequest request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        var response = await GenerateAsync(request, ct);
        yield return new LlmStreamChunk { Text = response.Text, IsCompleted = true };
    }

    private static ProcessStartInfo CreateStartInfo(
        string executable,
        IReadOnlyList<string> arguments,
        string workingDirectory)
    {
        var extension = Path.GetExtension(executable);
        var isCommandScript =
            string.Equals(extension, ".cmd", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(extension, ".bat", StringComparison.OrdinalIgnoreCase);

        var startInfo = new ProcessStartInfo
        {
            FileName = isCommandScript
                ? Environment.GetEnvironmentVariable("COMSPEC") ?? "cmd.exe"
                : executable,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardInputEncoding = Encoding.UTF8,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        if (isCommandScript)
        {
            startInfo.Arguments = $"/d /s /c \"{BuildCommandLine(executable, arguments)}\"";
        }
        else
        {
            foreach (var argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }
        }

        return startInfo;
    }

    private static string BuildPrompt(LlmRequest request)
    {
        var builder = new StringBuilder();
        builder.AppendLine(request.SystemPrompt);
        builder.AppendLine();
        builder.AppendLine(
            "This is a desktop companion conversation. For ordinary conversation, answer directly without inspecting files or running commands.");
        builder.AppendLine(
            "The sandbox is read-only. Never claim that files were changed. Keep the final answer suitable for display in a chat console.");

        if (request.History.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("Conversation history:");
            foreach (var message in request.History)
            {
                builder.Append(message.Role);
                builder.Append(": ");
                builder.AppendLine(message.Content);
            }
        }

        builder.AppendLine();
        builder.Append("user: ");
        builder.AppendLine(request.UserText);
        return builder.ToString();
    }

    private static string ResolveWorkingDirectory(string configuredPath)
    {
        var expandedPath = Environment.ExpandEnvironmentVariables(configuredPath ?? string.Empty);
        if (!string.IsNullOrWhiteSpace(expandedPath) && Directory.Exists(expandedPath))
        {
            return Path.GetFullPath(expandedPath);
        }

        var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        return Directory.Exists(documents) ? documents : AppContext.BaseDirectory;
    }

    private static string BuildCommandLine(string executable, IEnumerable<string> arguments)
    {
        return string.Join(" ", new[] { QuoteArgument(executable) }.Concat(arguments.Select(QuoteArgument)));
    }

    private static string QuoteArgument(string value)
    {
        return $"\"{value.Replace("\"", "\"\"")}\"";
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch
        {
        }
    }
}
