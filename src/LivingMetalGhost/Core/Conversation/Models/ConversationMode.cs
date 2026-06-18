namespace LivingMetalGhost.Core.Models;

/// <summary>
/// Ghost가 현재 어떤 성격의 대화를 수행하는지 나타낸다.
/// UI는 이 값을 보고 플로팅 말풍선, 롤플레잉, 워크벤치 같은 표현을 선택할 수 있다.
/// </summary>
public enum ConversationMode
{
    /// <summary>가벼운 일상 대화. 캐릭터 페르소나와 짧은 말풍선을 우선한다.</summary>
    Daily,

    /// <summary>AI소설/미연시/ORPG식 롤플레잉 진행. 허구 장면 상태를 우선한다.</summary>
    Story,

    /// <summary>실제 기반 검토/설계/작업 관리. 정확성과 승인 흐름을 우선한다.</summary>
    Advanced
}
