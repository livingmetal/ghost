namespace LivingMetalGhost.Core.Models;

/// <summary>
/// Character 응답 이후 순서대로 수행되는 Director/Memory 후처리 완료 신호다.
/// UI는 응답을 먼저 표시한 뒤 이 작업을 기다려 상태 패널을 갱신한다.
/// </summary>
public sealed record RoleplayTurnCompletion(Task Completion);
