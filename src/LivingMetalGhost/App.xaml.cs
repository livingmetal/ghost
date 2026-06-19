using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using LivingMetalGhost.Agents;
using LivingMetalGhost.AppCore.Roleplay;
using LivingMetalGhost.Core.Config;
using LivingMetalGhost.Core.Conversation;
using LivingMetalGhost.Core.Facts;
using LivingMetalGhost.Core.Facts.Meals.Kaist;
using LivingMetalGhost.Core.Presentation;
using LivingMetalGhost.Core.Reminders;
using LivingMetalGhost.Core.Roleplay;
using LivingMetalGhost.Core.Security;
using LivingMetalGhost.Core.Services;
using LivingMetalGhost.Core.Workbench;
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

        // WPF requires the owner Window to have been shown before another Window can use it as Owner.
        // Setting DataContext after Show prevents startup mode synchronization from creating child
        // windows while MainWindow is still in the not-yet-shown state.
        var mainViewModel = Services.GetRequiredService<MainViewModel>();

        // Story state persists scene/memory, but startup mode must always be the normal prompt mode.
        // Users can still re-enable roleplay explicitly from the menu after launch.
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

            // XAML 바인딩/레이아웃 예외는 한 프레임 안에서 여러 번 반복될 수 있다.
            // 모달 MessageBox를 매번 띄우면 오류창 폭주가 나므로 첫 1회만 알린다.
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
            // 마지막 안전망이다. 여기서 다시 터지면 원래 예외를 가리므로 조용히 버린다.
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
        services.AddSingleton<HiddenTraitScheduler>();
        services.AddSingleton<ConversationService>();
        services.AddSingleton<IRoleplayConversation>(
            serviceProvider => serviceProvider.GetRequiredService<ConversationService>());
        services.AddSingleton<RoleplaySessionController>();
        services.AddSingleton<ConversationLogService>();
        services.AddSingleton<SpriteDirector>();
        services.AddSingleton<FactStore>();
        services.AddSingleton<KaistMenuParser>();
        services.AddSingleton<KaistMunjiMenuService>();
        services.AddSingleton<ReminderStore>();
        services.AddSingleton<ReminderService>();
        services.AddSingleton<IntentRouter>();
        services.AddSingleton<SkillRegistry>();
        services.AddSingleton<ILlmProviderFactory, LlmProviderFactory>();
        services.AddSingleton<MockLlmProvider>();
        services.AddSingleton<OpenAiCompatibleProvider>();
        services.AddSingleton<CodexCliProvider>();
        services.AddSingleton<LmBotProvider>();
        services.AddSingleton<InstalledAppsProvider>();

        // 외부 작업 에이전트 계층(Provider 와 분리).
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
