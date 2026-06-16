using System.IO;
using System.Windows;
using LivingMetalGhost.Agents;
using LivingMetalGhost.Core.Config;
using LivingMetalGhost.Core.Presentation;
using LivingMetalGhost.Core.Security;
using LivingMetalGhost.Core.Services;
using LivingMetalGhost.Providers.Llm;
using LivingMetalGhost.Skills;
using LivingMetalGhost.UI.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LivingMetalGhost;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;
    private TrayIconService? _trayIconService;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var serviceCollection = new ServiceCollection();
        ConfigureServices(serviceCollection);
        Services = serviceCollection.BuildServiceProvider();

        var mainWindow = new MainWindow
        {
            DataContext = Services.GetRequiredService<MainViewModel>()
        };
        MainWindow = mainWindow;
        mainWindow.Show();

        _trayIconService = new TrayIconService(
            mainWindow.RestoreFromTray,
            mainWindow.OpenChatFromTray,
            mainWindow.OpenSettingsFromTray,
            Shutdown);
        mainWindow.TrayIconService = _trayIconService;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayIconService?.Dispose();
        base.OnExit(e);
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        var appDataRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "LivingMetalGhost");

        services.AddLogging();
        services.AddSingleton(new AppPaths(appDataRoot));
        services.AddSingleton<AppConfigLoader>();
        services.AddSingleton<DpapiSecretStore>();
        services.AddSingleton<ConversationService>();
        services.AddSingleton<ConversationLogService>();
        services.AddSingleton<SpriteDirector>();
        services.AddSingleton<IntentRouter>();
        services.AddSingleton<SkillRegistry>();
        services.AddSingleton<ILlmProviderFactory, LlmProviderFactory>();
        services.AddSingleton<MockLlmProvider>();
        services.AddSingleton<OpenAiCompatibleProvider>();
        services.AddSingleton<CodexCliProvider>();
        services.AddSingleton<LmBotProvider>();
        services.AddSingleton<InstalledAppsProvider>();

        // 외부 작업 에이전트 계층(Provider 와 분리).
        services.AddSingleton<IAgentExecutorFactory, AgentExecutorFactory>();
        services.AddSingleton<MockAgentExecutor>();
        services.AddSingleton<ClaudeCodeExecutor>();
        services.AddSingleton<CodexCliExecutor>();

        services.AddSingleton<ChatSkill>();
        services.AddSingleton<TranslateSkill>();
        services.AddSingleton<AppCommandSkill>();
        services.AddSingleton<CodingAgentSkill>();
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddTransient<ConversationLogViewModel>();
    }
}
