using System.IO;
using LivingMetalGhost.Core.Models;
using LivingMetalGhost.Core.Services;

namespace LivingMetalGhost.Agents;

/// <summary>
/// 외부 에이전트 실행 전에 workspace.json 정책을 실제로 적용하는 방어 계층.
/// 1차 범위는 작업 루트/읽기·쓰기 경로/승인 모드 검증이다.
/// </summary>
public sealed class AgentWorkspacePolicy
{
    private readonly WorkspaceStore _workspaceStore;

    public AgentWorkspacePolicy(WorkspaceStore workspaceStore)
    {
        _workspaceStore = workspaceStore;
    }

    public string GetEffectiveRoot(string? requestedRoot, string? fallbackRoot)
    {
        var settings = _workspaceStore.Load();
        return FirstNonEmpty(requestedRoot, settings.RootPath, fallbackRoot);
    }

    public bool TryValidateForExecution(
        AgentRequest request,
        string? fallbackRoot,
        out string workspaceRoot,
        out string error)
    {
        var settings = _workspaceStore.Load();
        var configuredRoot = FirstNonEmpty(request.WorkspaceRoot, settings.RootPath, fallbackRoot);
        if (!WorkspaceGuard.TryResolveRoot(configuredRoot, out workspaceRoot, out error))
        {
            return false;
        }

        if (!IsInsideWorkspaceRoot(settings, workspaceRoot, out error))
        {
            return false;
        }

        var requiredPaths = request.ApprovalMode switch
        {
            AgentApprovalMode.Apply or AgentApprovalMode.Execute => settings.AllowedWritePaths,
            _ => settings.AllowedReadPaths
        };

        if (!IsInsideAnyAllowedPath(workspaceRoot, requiredPaths, settings.RootPath))
        {
            var policyKind = request.ApprovalMode is AgentApprovalMode.Apply or AgentApprovalMode.Execute
                ? "쓰기"
                : "읽기";
            error = $"작업 루트가 workspace.json의 허용 {policyKind} 경로 밖입니다: {workspaceRoot}";
            return false;
        }

        if (request.ApprovalMode == AgentApprovalMode.Apply && settings.RequireApprovalForWrite is false)
        {
            // 승인 필요 플래그를 끄는 것은 허용하되, 로그상 명시되도록 성공 처리만 한다.
            return true;
        }

        if (request.ApprovalMode == AgentApprovalMode.Execute && settings.RequireApprovalForExecute is false)
        {
            return true;
        }

        if (request.ApprovalMode == AgentApprovalMode.Apply && settings.RequireApprovalForWrite)
        {
            return true;
        }

        if (request.ApprovalMode == AgentApprovalMode.Execute && settings.RequireApprovalForExecute)
        {
            return true;
        }

        return true;
    }

    public string BuildExecutionPolicyLabel(AgentRequest request, string? fallbackRoot)
    {
        var settings = _workspaceStore.Load();
        var root = GetEffectiveRoot(request.WorkspaceRoot, fallbackRoot);
        return $"""
            - Workspace: {settings.DisplayName} ({settings.WorkspaceId})
            - Root: {(string.IsNullOrWhiteSpace(root) ? "(미설정)" : root)}
            - Mode: {request.ApprovalMode}
            - Write approval required: {settings.RequireApprovalForWrite}
            - Execute approval required: {settings.RequireApprovalForExecute}
            """;
    }

    public bool AreChangedFilesAllowed(IEnumerable<string> changedFiles, string workspaceRoot, out IReadOnlyList<string> escapingPaths)
    {
        var settings = _workspaceStore.Load();
        var allowedWritePaths = settings.AllowedWritePaths.Count == 0 && !string.IsNullOrWhiteSpace(settings.RootPath)
            ? new[] { settings.RootPath }
            : settings.AllowedWritePaths;

        var escaping = new List<string>();
        foreach (var changedFile in changedFiles)
        {
            var fullPath = Path.IsPathRooted(changedFile)
                ? changedFile
                : Path.Combine(workspaceRoot, changedFile);
            if (!IsInsideAnyAllowedPath(fullPath, allowedWritePaths, settings.RootPath))
            {
                escaping.Add(changedFile);
            }
        }

        escapingPaths = escaping;
        return escaping.Count == 0;
    }

    private static bool IsInsideWorkspaceRoot(WorkspaceSettings settings, string candidateRoot, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(settings.RootPath))
        {
            return true;
        }

        if (WorkspaceGuard.IsInsideRoot(settings.RootPath, candidateRoot))
        {
            return true;
        }

        error = $"작업 루트가 workspace root 밖입니다. root={settings.RootPath}, requested={candidateRoot}";
        return false;
    }

    private static bool IsInsideAnyAllowedPath(
        string candidatePath,
        IReadOnlyList<string> allowedPaths,
        string workspaceRoot)
    {
        var paths = allowedPaths;
        if ((paths is null || paths.Count == 0) && !string.IsNullOrWhiteSpace(workspaceRoot))
        {
            paths = new[] { workspaceRoot };
        }

        if (paths is null || paths.Count == 0)
        {
            return false;
        }

        return paths.Any(path => WorkspaceGuard.IsInsideRoot(path, candidatePath));
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return Environment.ExpandEnvironmentVariables(value.Trim());
            }
        }

        return string.Empty;
    }
}
