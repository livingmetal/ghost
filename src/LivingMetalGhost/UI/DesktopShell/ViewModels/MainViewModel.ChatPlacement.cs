using LivingMetalGhost.Core.Config;

namespace LivingMetalGhost.UI.ViewModels;

public partial class MainViewModel
{
    public WindowPlacementSettings GetDailyChatWindowPlacement()
    {
        return _configLoader.Load().App.DailyChatWindow ?? new WindowPlacementSettings();
    }

    public void SaveDailyChatWindowPlacement(double left, double top)
    {
        var config = _configLoader.Load();
        config.App.DailyChatWindow ??= new WindowPlacementSettings();
        config.App.DailyChatWindow.HasPosition = true;
        config.App.DailyChatWindow.Left = left;
        config.App.DailyChatWindow.Top = top;
        _configLoader.Save(config);
    }
}
