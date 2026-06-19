namespace LivingMetalGhost.UI.Presentation;

public static class SubmittedInputPolicy
{
    public static bool ShouldClear(
        string currentInput,
        string submittedInput)
    {
        return string.Equals(
            currentInput,
            submittedInput,
            StringComparison.Ordinal);
    }
}
