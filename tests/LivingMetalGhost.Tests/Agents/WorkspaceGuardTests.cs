using LivingMetalGhost.Agents;
using Xunit;

namespace LivingMetalGhost.Tests.Agents;

public sealed class WorkspaceGuardTests : IDisposable
{
    private readonly string _root;

    public WorkspaceGuardTests()
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
    public void TryResolveRoot_RejectsEmptyRoot()
    {
        var resolved = WorkspaceGuard.TryResolveRoot("   ", out var resolvedRoot, out var error);

        Assert.False(resolved);
        Assert.Equal(string.Empty, resolvedRoot);
        Assert.Contains("workspace_root", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryResolveRoot_ResolvesExistingRoot()
    {
        var resolved = WorkspaceGuard.TryResolveRoot(_root, out var resolvedRoot, out var error);

        Assert.True(resolved);
        Assert.Equal(Path.GetFullPath(_root), resolvedRoot);
        Assert.Equal(string.Empty, error);
    }

    [Fact]
    public void IsInsideRoot_AcceptsRootAndChildren()
    {
        var child = Path.Combine(_root, "src", "file.txt");

        Assert.True(WorkspaceGuard.IsInsideRoot(_root, _root));
        Assert.True(WorkspaceGuard.IsInsideRoot(_root, child));
    }

    [Fact]
    public void IsInsideRoot_RejectsParentTraversalEscape()
    {
        var escaped = Path.Combine(_root, "..", "outside.txt");

        Assert.False(WorkspaceGuard.IsInsideRoot(_root, escaped));
    }

    [Fact]
    public void IsInsideRoot_RejectsSiblingWithSharedPrefix()
    {
        var siblingRoot = _root + "-other";
        var siblingPath = Path.Combine(siblingRoot, "file.txt");

        Assert.False(WorkspaceGuard.IsInsideRoot(_root, siblingPath));
    }

    [Fact]
    public void FindEscapingPaths_ReturnsOnlyPathsOutsideRoot()
    {
        var inside = Path.Combine(_root, "inside.txt");
        var outside = Path.Combine(_root, "..", "outside.txt");

        var escaping = WorkspaceGuard.FindEscapingPaths(_root, [inside, outside]);

        Assert.Single(escaping);
        Assert.Equal(outside, escaping[0]);
    }
}
