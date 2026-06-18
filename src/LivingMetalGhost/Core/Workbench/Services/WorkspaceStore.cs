using System.IO;
using System.Text;
using System.Text.Json;
using LivingMetalGhost.Core.Config;
using LivingMetalGhost.Core.Models;

namespace LivingMetalGhost.Core.Services;

/// <summary>
/// 고급 Workbench의 작업공간 설정을 저장한다.
/// 지금은 default workspace 1개를 관리하고, 나중에 workspace 다중화의 기준점이 된다.
/// </summary>
public sealed class WorkspaceStore
{
    private readonly string _workspaceRoot;
    private readonly string _settingsFile;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public WorkspaceStore(AppPaths paths)
    {
        _workspaceRoot = Path.Combine(paths.Root, "Workspaces", "default");
        _settingsFile = Path.Combine(_workspaceRoot, "workspace.json");
    }

    public string WorkspaceRoot => _workspaceRoot;
    public string SettingsFile => _settingsFile;

    public WorkspaceSettings Load()
    {
        if (!File.Exists(_settingsFile))
        {
            return CreateDefaultSettings();
        }

        try
        {
            var json = File.ReadAllText(_settingsFile, Encoding.UTF8);
            var settings = JsonSerializer.Deserialize<WorkspaceSettings>(json, _jsonOptions) ?? CreateDefaultSettings();
            return Normalize(settings);
        }
        catch (JsonException)
        {
            return CreateDefaultSettings();
        }
        catch (IOException)
        {
            return CreateDefaultSettings();
        }
        catch (UnauthorizedAccessException)
        {
            return CreateDefaultSettings();
        }
    }

    public void Save(WorkspaceSettings settings)
    {
        Directory.CreateDirectory(_workspaceRoot);
        settings = Normalize(settings);
        settings.UpdatedAt = DateTimeOffset.Now;
        var json = JsonSerializer.Serialize(settings, _jsonOptions);
        File.WriteAllText(_settingsFile, json, Encoding.UTF8);
    }

    public void EnsureWorkspaceFile()
    {
        Directory.CreateDirectory(_workspaceRoot);
        if (!File.Exists(_settingsFile))
        {
            Save(CreateDefaultSettings());
        }
    }

    public IReadOnlyList<string> FindLikelyWorkspaceRoots()
    {
        var candidates = new List<string>();
        AddCandidateFromParents(candidates, AppContext.BaseDirectory);
        AddCandidateFromParents(candidates, Environment.CurrentDirectory);

        return candidates
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(NormalizePath)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public bool TryDetectWorkspaceRoot(out string rootPath)
    {
        rootPath = FindLikelyWorkspaceRoots().FirstOrDefault() ?? string.Empty;
        return !string.IsNullOrWhiteSpace(rootPath);
    }

    public bool IsAlwaysApproved(string commandLine)
    {
        var normalized = NormalizeCommand(commandLine);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        return Load().AlwaysApprovedCommands.Any(command =>
            string.Equals(NormalizeCommand(command), normalized, StringComparison.OrdinalIgnoreCase));
    }

    public void AddAlwaysApprovedCommand(string commandLine)
    {
        var normalized = NormalizeCommand(commandLine);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return;
        }

        var settings = Load();
        if (settings.AlwaysApprovedCommands.Any(command =>
                string.Equals(NormalizeCommand(command), normalized, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        settings.AlwaysApprovedCommands = settings.AlwaysApprovedCommands
            .Concat(new[] { normalized })
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Save(settings);
    }

    public string BuildPromptContext()
    {
        var settings = Load();
        var readPaths = settings.AllowedReadPaths.Count == 0
            ? "none"
            : string.Join("\n", settings.AllowedReadPaths.Select(path => $"- {path}"));
        var writePaths = settings.AllowedWritePaths.Count == 0
            ? "none"
            : string.Join("\n", settings.AllowedWritePaths.Select(path => $"- {path}"));
        var commands = settings.AllowedCommands.Count == 0
            ? "none"
            : string.Join("\n", settings.AllowedCommands.Select(command => $"- {command}"));
        var alwaysApprovedCommands = settings.AlwaysApprovedCommands.Count == 0
            ? "none"
            : string.Join("\n", settings.AlwaysApprovedCommands.Select(command => $"- {command}"));

        return $"""
            Workspace policy:
            - Workspace id: {settings.WorkspaceId}
            - Display name: {settings.DisplayName}
            - Root path: {DisplayPath(settings.RootPath)}
            - Require approval for write: {settings.RequireApprovalForWrite}
            - Require approval for execute: {settings.RequireApprovalForExecute}
            - Allowed read paths:
            {readPaths}
            - Allowed write paths:
            {writePaths}
            - Allowed commands:
            {commands}
            - Always-approved commands:
            {alwaysApprovedCommands}
            """;
    }

    private WorkspaceSettings CreateDefaultSettings()
    {
        var defaultRoot = TryFindLikelyGhostRoot() ?? string.Empty;
        return new WorkspaceSettings
        {
            WorkspaceId = "default",
            DisplayName = "Default Workspace",
            RootPath = defaultRoot,
            AllowedReadPaths = string.IsNullOrWhiteSpace(defaultRoot) ? Array.Empty<string>() : new[] { defaultRoot },
            AllowedWritePaths = string.IsNullOrWhiteSpace(defaultRoot) ? Array.Empty<string>() : new[] { defaultRoot },
            AllowedCommands = new[]
            {
                "git status",
                "git branch",
                "git diff",
                "git log",
                "git remote",
                "git fetch",
                "git pull",
                "dotnet build",
                "dotnet test"
            },
            AlwaysApprovedCommands = Array.Empty<string>(),
            RequireApprovalForWrite = true,
            RequireApprovalForExecute = true,
            UpdatedAt = DateTimeOffset.Now
        };
    }

    private static WorkspaceSettings Normalize(WorkspaceSettings settings)
    {
        var rootPath = NormalizePath(settings.RootPath);
        var readPaths = NormalizePathList(settings.AllowedReadPaths);
        var writePaths = NormalizePathList(settings.AllowedWritePaths);
        var commands = NormalizeCommandList(settings.AllowedCommands);
        var alwaysApprovedCommands = NormalizeCommandList(settings.AlwaysApprovedCommands);

        if (!string.IsNullOrWhiteSpace(rootPath))
        {
            if (readPaths.Count == 0)
            {
                readPaths = new[] { rootPath };
            }

            if (writePaths.Count == 0)
            {
                writePaths = new[] { rootPath };
            }
        }

        settings.WorkspaceId = string.IsNullOrWhiteSpace(settings.WorkspaceId) ? "default" : settings.WorkspaceId.Trim();
        settings.DisplayName = string.IsNullOrWhiteSpace(settings.DisplayName)
            ? settings.WorkspaceId
            : settings.DisplayName.Trim();
        settings.RootPath = rootPath;
        settings.AllowedReadPaths = readPaths;
        settings.AllowedWritePaths = writePaths;
        settings.AllowedCommands = commands;
        settings.AlwaysApprovedCommands = alwaysApprovedCommands;
        return settings;
    }

    private static IReadOnlyList<string> NormalizePathList(IReadOnlyList<string>? paths)
    {
        if (paths is null)
        {
            return Array.Empty<string>();
        }

        return paths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(NormalizePath)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyList<string> NormalizeCommandList(IReadOnlyList<string>? commands)
    {
        if (commands is null)
        {
            return Array.Empty<string>();
        }

        return commands
            .Where(command => !string.IsNullOrWhiteSpace(command))
            .Select(NormalizeCommand)
            .Where(command => !string.IsNullOrWhiteSpace(command))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string NormalizeCommand(string? commandLine)
    {
        return string.Join(' ', (commandLine ?? string.Empty)
            .Trim()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static string NormalizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        try
        {
            return Path.GetFullPath(path.Trim());
        }
        catch
        {
            return path.Trim();
        }
    }

    private static string DisplayPath(string path)
    {
        return string.IsNullOrWhiteSpace(path) ? "not set" : path;
    }

    private static string? TryFindLikelyGhostRoot()
    {
        var candidates = new List<string>();
        AddCandidateFromParents(candidates, AppContext.BaseDirectory);
        AddCandidateFromParents(candidates, Environment.CurrentDirectory);
        return candidates.FirstOrDefault();
    }

    private static void AddCandidateFromParents(ICollection<string> candidates, string? startPath)
    {
        if (string.IsNullOrWhiteSpace(startPath))
        {
            return;
        }

        try
        {
            var directory = new DirectoryInfo(startPath);
            if (File.Exists(startPath))
            {
                directory = new FileInfo(startPath).Directory!;
            }

            while (directory is not null)
            {
                if (IsWorkspaceRoot(directory.FullName))
                {
                    candidates.Add(directory.FullName);
                    return;
                }

                directory = directory.Parent;
            }
        }
        catch
        {
        }
    }

    private static bool IsWorkspaceRoot(string path)
    {
        return File.Exists(Path.Combine(path, "LivingMetalGhost.sln")) ||
               Directory.Exists(Path.Combine(path, ".git"));
    }
}
