namespace LivingMetalGhost.Core.Facts.Meals;

public sealed record MealMenu(
    MealSlot Slot,
    string Label,
    string? ServiceTime,
    int? PriceWon,
    int? Calories,
    IReadOnlyList<DiningItem> Items);
