using LivingMetalGhost.Agents;
using LivingMetalGhost.Core.Config;
using LivingMetalGhost.Core.Models;
using LivingMetalGhost.Core.Services;
using Xunit;

namespace LivingMetalGhost.Tests.Agents;

public sealed class CommandPolicyServiceTests : IDisposable
{
    private readonly string _root;

    public CommandPolicyServiceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "LivingMetalGhost.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
        catch
        {
        }
    }

    [Fact]
    public void SafeReadCommand_CanAutoRunWithoutApproval()
    {
        var policy = CreatePolicy("git status");

        var decision = policy.Evaluate("  git   status   --short  ");

        Assert.Equal(CommandRiskLevel.SafeRead, decision.RiskLevel);
        Assert.True(decision.CanAutoRun);
        Assert.False(decision.RequiresApproval);
        Assert.False(decision.RequiresStrongApproval);
        Assert.True(decision.IsAllowedByWorkspace);
    }

    [Fact]
    public void NetworkReadCommand_RequiresApprovalAndWorkspaceAllowList()
    {
        var policy = CreatePolicy("git fetch");

        var decision = policy.Evaluate("git fetch origin");

        Assert.Equal(CommandRiskLevel.NetworkRead, decision.RiskLevel);
        Assert.False(decision.CanAutoRun);
        Assert.True(decision.RequiresApproval);
        Assert.False(decision.RequiresStrongApproval);
        Assert.True(decision.IsAllowedByWorkspace);
    }

    [Fact]
    public void WorkspaceWriteCommand_RequiresApprovalAndWorkspaceAllowList()
    {
        var policy = CreatePolicy("dotnet build");

        var decision = policy.Evaluate("dotnet build src/LivingMetalGhost/LivingMetalGhost.csproj");

        Assert.Equal(CommandRiskLevel.WorkspaceWrite, decision.RiskLevel);
        Assert.False(decision.CanAutoRun);
        Assert.True(decision.RequiresApproval);
        Assert.False(decision.RequiresStrongApproval);
        Assert.True(decision.IsAllowedByWorkspace);
    }

    [Fact]
    public void DangerousCommand_RequiresStrongApprovalAndIsNotWorkspaceAllowed()
    {
        var policy = CreatePolicy("git reset");

        var decision = policy.Evaluate("git reset --hard HEAD~1");

        Assert.Equal(CommandRiskLevel.Dangerous, decision.RiskLevel);
        Assert.False(decision.CanAutoRun);
        Assert.True(decision.RequiresApproval);
        Assert.True(decision.RequiresStrongApproval);
        Assert.False(decision.IsAllowedByWorkspace);
    }

    [Fact]
    public void UnknownCommand_IsBlockedEvenWhenWorkspaceHasOtherAllowedCommands()
    {
        var policy = CreatePolicy("git status", "dotnet build");

        var decision = policy.Evaluate("powershell Get-ChildItem");

        Assert.Equal(CommandRiskLevel.Blocked, decision.RiskLevel);
        Assert.False(decision.CanAutoRun);
        Assert.True(decision.RequiresApproval);
        Assert.True(decision.RequiresStrongApproval);
        Assert.False(decision.IsAllowedByWorkspace);
    }

    [Fact]
    public void EmptyCommand_IsBlocked()
    {
        var policy = CreatePolicy("git status");

        var decision = policy.Evaluate("   ");

        Assert.Equal(CommandRiskLevel.Blocked, decision.RiskLevel);
        Assert.False(decision.CanAutoRun);
        Assert.True(decision.RequiresStrongApproval);
        Assert.False(decision.IsAllowedByWorkspace);
    }

    private CommandPolicyService CreatePolicy(params string[] allowedCommands)
    {
        var store = new WorkspaceStore(new AppPaths(_root));
        store.Save(new WorkspaceSettings
        {
            RootPath = _root,
            AllowedReadPaths = [_root],
            AllowedWritePaths = [_root],
            AllowedCommands = allowedCommands,
            AlwaysApprovedCommands = []
        });

        return new CommandPolicyService(store);
    }
}
