using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using LivingMetalGhost.Agents;
using LivingMetalGhost.AppCore.Conversation;
using LivingMetalGhost.AppCore.Desktop;
using LivingMetalGhost.AppCore.Roleplay;
using LivingMetalGhost.AppCore.SlashAgents;
using LivingMetalGhost.AppCore.SlashAgents.Capabilities;
using LivingMetalGhost.Core.Config;
using LivingMetalGhost.Core.Conversation;
using LivingMetalGhost.Core.Facts;
using LivingMetalGhost.Core.Facts.Meals.Kaist;
using LivingMetalGhost.Core.Facts.Weather;
using LivingMetalGhost.Core.Presentation;
using LivingMetalGhost.Core.Reminders;
using LivingMetalGhost.Core.Roleplay;
using LivingMetalGhost.Core.Security;
using LivingMetalGhost.Core.Services;
using LivingMetalGhost.Core.Workbench;
using LivingMetalGhost.Providers.Llm;
using LivingMetalGhost.Skills;
using LivingMetalGhost.UI.Presentation;
using LivingMetalGhost.UI.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LivingMetalGhost;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;
    private TrayIconService? _trayIconService;
    private static int _dispatcherExceptionCount;

    protected override void OnStartup(StartupEventArgs e)
    {
        RegisterExceptionHandlers();
        base.OnStartup(e);

        var serviceCollection = new ServiceCollection();
        ConfigureServices(serviceCollection);
        Services = serviceCollection.BuildServiceProvider();
        Services.GetRequiredService<AdvancedSessionLogService>().EnsureWorkspaceFiles();
        Services.GetRequiredService<WorkspaceStore>().EnsureWorkspaceFile();
        Services.GetRequiredService<ReminderService>().Start();

        var mainWindow = new MainWindow();
        MainWindow = mainWindow;
        mainWindow.Show();

        var mainViewModel = Services.GetRequiredService<MainViewModel>();
        mainViewModel.SetStoryMode(false);
        mainWindow.DataContext = mainViewModel;

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

    private static void RegisterExceptionHandlers()
    {
        Current.DispatcherUnhandledException += (_, e) =>
        {
            WriteCrashLog("dispatcher", e.Exception);
            e.Handled = true;
            if (Interlocked.Increment(ref _dispatcherExceptionCount) == 1)
            {
                MessageBox.Show(
                    "Ghost가 UI 예외를 잡았어요. 프로그램은 계속 실행됩니다.\n" +
                    "자세한 내용은 %APPDATA%\\LivingMetalGhost\\Logs 의 crash 로그를 확인하세요.",
                    "LivingMetalGhost 오류",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        };

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            WriteCrashLog("appdomain", e.ExceptionObject as Exception);
        };

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            WriteCrashLog("task", e.Exception);
            e.SetObserved();
        };
    }

    public static void WriteCrashLog(string source, Exception? exception)
    {
        try
        {
            var logRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "LivingMetalGhost",
                "Logs");
            Directory.CreateDirectory(logRoot);
            var filePath = Path.Combine(logRoot, $"crash-{DateTime.Now:yyyyMMdd-HHmmss-fff}-{source}.log");

            var builder = new StringBuilder();
            builder.AppendLine($"Source: {source}");
            builder.AppendLine($"Time: {DateTimeOffset.Now:O}");
            builder.AppendLine($"Version: {typeof(App).Assembly.GetName().Version}");
            builder.AppendLine();
            builder.AppendLine(exception?.ToString() ?? "Unknown non-Exception crash object.");

            File.WriteAllText(filePath, builder.ToString(), Encoding.UTF8);
        }
        catch
        {
        }
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
        services.AddSingleton<StoryStateStore>();
        services.AddSingleton<StoryPlanStore>();
        services.AddSingleton<RoleplayStateUpdater>();
        services.AddSingleton<RoleplayMemoryDigestService>();
        services.AddSingleton<AdvancedSessionLogService>();
        services.AddSingleton<AdvancedConversationSupport>();
        services.AddSingleton<ProjectMemoryStore>();
        services.AddSingleton<WorkspaceStore>();
        services.AddSingleton<Core.Workspace.WorkspaceReadService>();
        services.AddSingleton<Core.Workspace.WorkspaceContextBuilder>();
        services.AddSingleton<Core.Workspace.DiffService>();
        services.AddSingleton<Core.Workspace.PatchApplyService>();
        services.AddSingleton<Core.Workspace.PatchReviewService>();
        services.AddSingleton<AdvancedPromptPolicy>();
        services.AddSingleton<PromptAssembler>();
        services.AddSingleton<ConversationHistoryStore>();
        services.AddSingleton<ExternalConversationTurnRecorder>();
        services.AddSingleton<HiddenTraitScheduler>();
        services.AddSingleton<ConversationRequestFactory>();
        services.AddSingleton<ConversationResponseProcessor>();
        services.AddSingleton<ConversationService>();
        services.AddSingleton<IRoleplayConversation>(
            serviceProvider => serviceProvider.GetRequiredService<ConversationService>());
        services.AddSingleton<RoleplaySessionController>();
        services.AddSingleton<ConversationLogService>();
        services.AddSingleton<CompanionConversationController>();
        services.AddSingleton<ConversationTurnLogWriter>();
        services.AddSingleton<DesktopRuntimeSettingsService>();
        services.AddSingleton<CharacterMoodResolver>();
        services.AddSingleton<SpriteDirector>();
        services.AddSingleton<AssistantMessagePresenter>();
        services.AddSingleton<FactStore>();
        services.AddSingleton<KaistMenuParser>();
        services.AddSingleton<KaistMunjiMenuService>();
        services.AddSingleton(_ => new WeatherService());
        services.AddSingleton<ReminderStore>();
        services.AddSingleton<ReminderService>();
        services.AddSingleton<SlashIntentPlanner>();
        services.AddSingleton<SlashAgentResponseComposer>();
        services.AddSingleton<ISlashCapabilityHandler, DateCapabilityHandler>();
        services.AddSingleton<ISlashCapabilityHandler, TimeCapabilityHandler>();
        services.AddSingleton<ISlashCapabilityHandler, MealCapabilityHandler>();
        services.AddSingleton<ISlashCapabilityHandler, WeatherCapabilityHandler>();
        services.AddSingleton<ISlashCapabilityHandler, ReminderCapabilityHandler>();
        services.AddSingleton<SlashAgentService>();
        services.AddSingleton<IntentRouter>();
        services.AddSingleton<SkillRegistry>();
        services.AddSingleton<ILlmProviderFactory, LlmProviderFactory>();
        services.AddSingleton<MockLlmProvider>();
        services.AddSingleton<OpenAiCompatibleProvider>();
        services.AddSingleton<CodexCliProvider>();
        services.AddSingleton<LmBotProvider>();
        services.AddSingleton<InstalledAppsProvider>();

        services.AddSingleton<AgentWorkspacePolicy>();
        services.AddSingleton<CommandPolicyService>();
        services.AddSingleton<IAgentExecutorFactory, AgentExecutorFactory>();
        services.AddSingleton<MockAgentExecutor>();
        services.AddSingleton<ClaudeCodeExecutor>();
        services.AddSingleton<CodexCliExecutor>();

        services.AddSingleton<ChatSkill>();
        services.AddSingleton<SlashIntentSkill>();
        services.AddSingleton<TranslateSkill>();
        services.AddSingleton<AppCommandSkill>();
        services.AddSingleton<GitCommandSkill>();
        services.AddSingleton<CodingAgentSkill>();
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddTransient<ConversationLogViewModel>();
    }
}
