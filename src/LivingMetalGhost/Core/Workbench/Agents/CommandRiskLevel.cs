namespace LivingMetalGhost.Agents;

/// <summary>
/// 로컬 명령 실행 위험도. Workbench는 이 값을 기준으로 자동 실행/승인/차단을 결정한다.
/// </summary>
public enum CommandRiskLevel
{
    SafeRead,
    NetworkRead,
    WorkspaceWrite,
    Dangerous,
    Blocked
}

public sealed class CommandPolicyDecision
{
    public CommandRiskLevel RiskLevel { get; init; }
    public bool CanAutoRun { get; init; }
    public bool RequiresApproval { get; init; }
    public bool RequiresStrongApproval { get; init; }
    public bool IsAllowedByWorkspace { get; init; }
    public string Reason { get; init; } = string.Empty;
}
