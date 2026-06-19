namespace LivingMetalGhost.AppCore.Desktop;

public readonly record struct DesktopBounds(
    double Left,
    double Top,
    double Width,
    double Height)
{
    public double Right => Left + Width;
    public double Bottom => Top + Height;
}

public readonly record struct DesktopSize(double Width, double Height);

public readonly record struct DesktopPosition(double Left, double Top);

public static class CompanionWindowPlacement
{
    public static DesktopPosition PositionChat(
        DesktopBounds workArea,
        DesktopBounds companion,
        DesktopSize chat)
    {
        var horizontalOverlap = Math.Clamp(
            companion.Width * 0.38,
            128,
            220);
        var verticalOffset = Math.Clamp(
            companion.Height * 0.08,
            12,
            48);

        var preferredLeft =
            companion.Left - chat.Width + horizontalOverlap;
        if (preferredLeft < workArea.Left)
        {
            preferredLeft =
                companion.Left + companion.Width - horizontalOverlap;
        }

        return Clamp(
            workArea,
            new DesktopPosition(
                preferredLeft,
                companion.Top + verticalOffset),
            chat);
    }

    public static DesktopPosition Center(
        DesktopBounds workArea,
        DesktopSize window)
    {
        return Clamp(
            workArea,
            new DesktopPosition(
                workArea.Left + (workArea.Width - window.Width) / 2,
                workArea.Top + (workArea.Height - window.Height) / 2),
            window);
    }

    public static DesktopPosition BottomRight(
        DesktopBounds workArea,
        DesktopSize window,
        double margin)
    {
        return Clamp(
            workArea,
            new DesktopPosition(
                workArea.Right - window.Width - margin,
                workArea.Bottom - window.Height - margin),
            window);
    }

    public static DesktopPosition Clamp(
        DesktopBounds workArea,
        DesktopPosition position,
        DesktopSize window)
    {
        var maximumLeft = Math.Max(
            workArea.Left,
            workArea.Right - window.Width);
        var maximumTop = Math.Max(
            workArea.Top,
            workArea.Bottom - window.Height);

        return new DesktopPosition(
            Math.Clamp(position.Left, workArea.Left, maximumLeft),
            Math.Clamp(position.Top, workArea.Top, maximumTop));
    }
}
