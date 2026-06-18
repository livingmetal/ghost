using LivingMetalGhost.Core.Services;

namespace LivingMetalGhost.Agents;

/// <summary>
/// Workbench에서 실행하려는 로컬 명령을 위험도별로 분류한다.
/// SafeRead는 자동 실행 가능, NetworkRead/WorkspaceWrite는 승인 필요,
/// Dangerous는 강한 승인 또는 차단 대상으로 본다.
/// </summary>
public sealed class CommandPolicyService
{
    private static readonly string[] BuiltInSafeReadCommands =
    [
        "git status",
        "git status --short",
        "git status --porcelain",
        "git branch --show-current",
        "git diff",
        "git diff --stat",
        "git log --oneline",
        "git log --oneline -n",
        "git remote -v"
    ];

    private static readonly string[] NetworkReadCommands =
    [
        "git fetch",
        "git ls-remote"
    ];

    private static readonly string[] WorkspaceWriteCommands =
    [
        "git pull",
        "git merge",
        "git checkout",
        "git switch",
        "git stash",
        "dotnet restore",
        "dotnet build",
        "dotnet test"
    ];

    private static readonly string[] DangerousCommands =
    [
        "git reset --hard",
        "git clean",
        "rm ",
        "del ",
        "rmdir ",
        "Remove-Item",
        "powershell -ExecutionPolicy Bypass",
        "cmd /c",
        "format ",
        "diskpart"
    ];

    private readonly WorkspaceStore _workspaceStore;

    public CommandPolicyService(WorkspaceStore workspaceStore)
    {
        _workspaceStore = workspaceStore;
    }

    public CommandPolicyDecision Evaluate(string commandLine)
    {
        var command = Normalize(commandLine);
        if (string.IsNullOrWhiteSpace(command))
        {
            return Blocked("빈 명령은 실행할 수 없습니다.");
        }

        if (DangerousCommands.Any(pattern => ContainsCommand(command, pattern)))
        {
            return new CommandPolicyDecision
            {
                RiskLevel = CommandRiskLevel.Dangerous,
                CanAutoRun = false,
                RequiresApproval = true,
                RequiresStrongApproval = true,
                IsAllowedByWorkspace = false,
                Reason = "로컬 변경 삭제, 셸 우회, 시스템 변경 가능성이 있는 위험 명령입니다."
            };
        }

        if (MatchesAny(command, BuiltInSafeReadCommands))
        {
            return new CommandPolicyDecision
            {
                RiskLevel = CommandRiskLevel.SafeRead,
                CanAutoRun = true,
                RequiresApproval = false,
                RequiresStrongApproval = false,
                IsAllowedByWorkspace = true,
                Reason = "읽기 전용 명령입니다."
            };
        }

        if (MatchesAny(command, NetworkReadCommands))
        {
            return new CommandPolicyDecision
            {
                RiskLevel = CommandRiskLevel.NetworkRead,
                CanAutoRun = false,
                RequiresApproval = true,
                RequiresStrongApproval = false,
                IsAllowedByWorkspace = IsAllowedByWorkspace(command),
                Reason = "네트워크 접근이 필요한 읽기 명령입니다. 실행 전 승인이 필요합니다."
            };
        }

        if (MatchesAny(command, WorkspaceWriteCommands))
        {
            return new CommandPolicyDecision
            {
                RiskLevel = CommandRiskLevel.WorkspaceWrite,
                CanAutoRun = false,
                RequiresApproval = true,
                RequiresStrongApproval = false,
                IsAllowedByWorkspace = IsAllowedByWorkspace(command),
                Reason = "워크스페이스 파일이나 Git 상태를 바꿀 수 있는 명령입니다. 실행 전 승인이 필요합니다."
            };
        }

        return new CommandPolicyDecision
        {
            RiskLevel = CommandRiskLevel.Blocked,
            CanAutoRun = false,
            RequiresApproval = true,
            RequiresStrongApproval = true,
            IsAllowedByWorkspace = false,
            Reason = "workspace.json의 명령 정책에 없는 명령입니다. 자동 실행하지 않습니다."
        };
    }

    private bool IsAllowedByWorkspace(string command)
    {
        var settings = _workspaceStore.Load();
        if (settings.AllowedCommands.Count == 0)
        {
            return false;
        }

        return settings.AllowedCommands
            .Select(Normalize)
            .Any(allowed => command.StartsWith(allowed, StringComparison.OrdinalIgnoreCase));
    }

    private static CommandPolicyDecision Blocked(string reason) => new()
    {
        RiskLevel = CommandRiskLevel.Blocked,
        CanAutoRun = false,
        RequiresApproval = true,
        RequiresStrongApproval = true,
        IsAllowedByWorkspace = false,
        Reason = reason
    };

    private static bool MatchesAny(string command, IEnumerable<string> patterns)
    {
        return patterns.Any(pattern =>
        {
            var normalizedPattern = Normalize(pattern);
            return command.Equals(normalizedPattern, StringComparison.OrdinalIgnoreCase) ||
                   command.StartsWith(normalizedPattern + " ", StringComparison.OrdinalIgnoreCase);
        });
    }

    private static bool ContainsCommand(string command, string pattern)
    {
        var normalizedPattern = Normalize(pattern);
        return command.Contains(normalizedPattern, StringComparison.OrdinalIgnoreCase);
    }

    private static string Normalize(string commandLine)
    {
        return string.Join(' ', (commandLine ?? string.Empty)
            .Trim()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}
