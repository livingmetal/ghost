namespace LivingMetalGhost.Core.Models;

/// <summary>
/// 고급 Workbench가 어느 작업공간을 기준으로 판단하고, 나중에 어떤 경로/명령을 허용할지 정의한다.
/// </summary>
public sealed class WorkspaceSettings
{
    public string WorkspaceId { get; set; } = "default";
    public string DisplayName { get; set; } = "Default Workspace";
    public string RootPath { get; set; } = string.Empty;
    public IReadOnlyList<string> AllowedReadPaths { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> AllowedWritePaths { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> AllowedCommands { get; set; } = new[]
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
    };
    public IReadOnlyList<string> AlwaysApprovedCommands { get; set; } = Array.Empty<string>();
    public bool RequireApprovalForWrite { get; set; } = true;
    public bool RequireApprovalForExecute { get; set; } = true;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.Now;
}
