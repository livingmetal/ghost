namespace LivingMetalGhost.Core.Facts.Meals;

public sealed record KaistMenuDocument(
    string CampusCode,
    string CampusName,
    DateOnly MenuDate,
    DateTimeOffset ObservedAt,
    string SourceUrl,
    MealMenu? Breakfast,
    MealMenu? Lunch,
    MealMenu? Dinner,
    IReadOnlyList<string> Notices);
