using Xunit;

namespace LivingMetalGhost.Tests.Core.Roleplay;

public sealed class RoleplaySyntaxDocumentationTests
{
    [Fact]
    public void PromptAssemblerAndDocs_DescribeTheSameRoleplayActionDelimiter()
    {
        var root = FindRepositoryRoot();
        var promptAssembler = File.ReadAllText(Path.Combine(
            root,
            "src",
            "LivingMetalGhost",
            "Core",
            "Conversation",
            "Services",
            "PromptAssembler.cs"));
        var readme = File.ReadAllText(Path.Combine(root, "README.md"));
        var syntaxGuide = File.ReadAllText(Path.Combine(root, "plans", "roleplay-input-syntax.md"));

        Assert.Contains("Text inside double asterisks is visible action or scene narration", promptAssembler);
        Assert.Contains("Single-asterisk text is not action syntax", promptAssembler);
        Assert.DoesNotContain("Text inside *asterisks* is visible action", promptAssembler);
        Assert.DoesNotContain("Wrap each action in single asterisks", promptAssembler);

        Assert.Contains("**text**       -> visible action / narration", readme);
        Assert.Contains("Single-asterisk text", readme);
        Assert.DoesNotContain("*text*        -> visible action / narration", readme);

        Assert.Contains("**text**        -> visible action or scene narration", syntaxGuide);
        Assert.Contains("Single asterisks are intentionally not action syntax", syntaxGuide);
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "LivingMetalGhost.sln")) ||
                File.Exists(Path.Combine(current.FullName, "README.md")) &&
                Directory.Exists(Path.Combine(current.FullName, "src", "LivingMetalGhost")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root from test output directory.");
    }
}
