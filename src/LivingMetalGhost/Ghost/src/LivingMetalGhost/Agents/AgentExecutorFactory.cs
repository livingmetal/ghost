namespace LivingMetalGhost.Agents;

public sealed class AgentExecutorFactory : IAgentExecutorFactory
{
    private readonly MockAgentExecutor _mock;
    private readonly ClaudeCodeExecutor _claudeCode;
    private readonly CodexCliExecutor _codexCli;

    public AgentExecutorFactory(
        MockAgentExecutor mock,
        ClaudeCodeExecutor claudeCode,
        CodexCliExecutor codexCli)
    {
        _mock = mock;
        _claudeCode = claudeCode;
        _codexCli = codexCli;
    }

    public IAgentExecutor Create(string executorName) =>
        (executorName ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "claude-code" or "claude" => _claudeCode,
            "codex-cli" or "codex" => _codexCli,
            "mock" => _mock,
            // 알 수 없는 값은 가장 안전한 mock 으로 폴백.
            _ => _mock
        };
}
