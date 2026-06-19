using LivingMetalGhost.Core.Facts.Meals.Kaist;

namespace LivingMetalGhost.AppCore.SlashAgents.Capabilities;

public sealed class MealCapabilityHandler : ISlashCapabilityHandler
{
    private readonly KaistMunjiMenuService _menuService;

    public MealCapabilityHandler(KaistMunjiMenuService menuService)
    {
        _menuService = menuService;
    }

    public string Capability => SlashCapabilities.Meal;

    public async Task<SlashCapabilityResult> ExecuteAsync(
        SlashIntentPlan plan,
        CancellationToken cancellationToken)
    {
        var slotText = plan.MealSlot switch
        {
            "breakfast" => "조식",
            "lunch" => "점심",
            "dinner" => "저녁",
            _ => "오늘 식단"
        };
        var facts = await _menuService.GetTodayMenuTextAsync(
            $"문지캠퍼스 {slotText}",
            cancellationToken);
        return new SlashCapabilityResult(Capability, facts);
    }
}
