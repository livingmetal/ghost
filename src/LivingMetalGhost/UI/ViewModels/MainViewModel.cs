using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LivingMetalGhost.Core.Config;
using LivingMetalGhost.Core.Models;
using LivingMetalGhost.Core.Services;
using LivingMetalGhost.Providers.Llm;
using LivingMetalGhost.Skills;

namespace LivingMetalGhost.UI.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly AppConfigLoader _configLoader;
    private readonly IntentRouter _intentRouter;
    private readonly SettingsViewModel _settingsViewModel;
    private readonly ConversationService _conversationService;
    private readonly ConversationLogService _conversationLogService;
    private bool _isResponding;

    [ObservableProperty]
    private string bubbleText = "기다리고 있어요.";

    [ObservableProperty]
    private string inputText = string.Empty;

    [ObservableProperty]
    private string characterMood = "idle";

    [ObservableProperty]
    private string characterStateLabel = "IDLE";

    [ObservableProperty]
    private bool isCharacterSpeaking;

    [ObservableProperty]
    private string selectedCharacterId = "ssuang";

    [ObservableProperty]
    private string characterDisplayName = "쑝";

    [ObservableProperty]
    private string selectedCharacterSizePresetId = "normal";

    [ObservableProperty]
    private double characterScale = 1.0;

    [ObservableProperty]
    private string selectedCharacterFramingPresetId = "full-body";

    [ObservableProperty]
    private bool isAdvancedMode;

    [ObservableProperty]
    private bool isLocalLmAvailable;

    /// <summary>
    /// 현재 설정된 고급 대화 provider 를 기준으로 고급 모드 토글 가능 여부.
    /// lmbot: 로컬 CLI 감지 결과 / 그 외(API 기반): 항상 true.
    /// </summary>
    [ObservableProperty]
    private bool isAdvancedModeAvailable = true;

    [ObservableProperty]
    private int proactiveSettingsRevision;

    public MainViewModel(
        AppConfigLoader configLoader,
        IntentRouter intentRouter,
        SettingsViewModel settingsViewModel,
        ConversationService conversationService,
        ConversationLogService conversationLogService)
    {
        _configLoader = configLoader;
        _intentRouter = intentRouter;
        _settingsViewModel = settingsViewModel;
        _conversationService = conversationService;
        _conversationLogService = conversationLogService;
        RefreshSelectedCharacter();
        _ = RefreshLocalLmAvailabilityAsync();
    }

    public async Task RefreshLocalLmAvailabilityAsync()
    {
        var advancedProvider = _configLoader.Load().AdvancedLlm.Provider;
        var isInstalledApps =
            string.Equals(advancedProvider, "installed-apps", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(advancedProvider, "installed_apps", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(advancedProvider, "apps", StringComparison.OrdinalIgnoreCase);
        var needsLocalLm = string.Equals(advancedProvider, "lmbot", StringComparison.OrdinalIgnoreCase);

        if (isInstalledApps)
        {
            InstalledAppDetector.Invalidate();
            var info = await InstalledAppDetector.DetectAsync();
            IsLocalLmAvailable = info.IsAnyAvailable;
            IsAdvancedModeAvailable = info.IsAnyAvailable;
        }
        else if (needsLocalLm)
        {
            LocalLmDetector.Invalidate();
            var info = await LocalLmDetector.DetectAsync();
            IsLocalLmAvailable = info.IsAvailable;
            IsAdvancedModeAvailable = info.IsAvailable;
        }
        else
        {
            IsLocalLmAvailable = false;
            IsAdvancedModeAvailable = true;
        }

        if (!IsAdvancedModeAvailable && IsAdvancedMode)
        {
            IsAdvancedMode = false;
        }
    }

    public ObservableCollection<ChatMessage> Messages { get; } = [];

    public string ActiveProviderLabel
    {
        get
        {
            var config = _configLoader.Load();
            return $"Provider: {config.Llm.Provider} / Model: {config.Llm.Model}";
        }
    }

    public (bool Enabled, int MinMinutes, int MaxMinutes) GetProactiveChatSettings()
    {
        var config = _configLoader.Load();
        var legacyInterval = Math.Clamp(config.App.ProactiveChatIntervalMinutes, 5, 240);
        var minMinutes = config.App.ProactiveChatMinMinutes <= 0
            ? legacyInterval
            : Math.Clamp(config.App.ProactiveChatMinMinutes, 5, 240);
        var maxMinutes = config.App.ProactiveChatMaxMinutes <= 0
            ? legacyInterval
            : Math.Clamp(config.App.ProactiveChatMaxMinutes, 5, 240);

        return (
            config.App.EnableProactiveChat,
            Math.Min(minMinutes, maxMinutes),
            Math.Max(minMinutes, maxMinutes));
    }

    public void SaveCharacterScale()
    {
        var config = _configLoader.Load();
        config.App.CharacterProfiles ??= [];
        var character = CharacterCatalog.Get(SelectedCharacterId);
        if (!config.App.CharacterProfiles.TryGetValue(character.Id, out var profile))
        {
            profile = new CharacterPromptSettings();
            config.App.CharacterProfiles[character.Id] = profile;
        }

        profile.CharacterScale = Math.Clamp(CharacterScale, 0.55, 2.0);
        _configLoader.Save(config);
    }

    [RelayCommand]
    private async Task SendAsync()
    {
        if (_isResponding)
        {
            return;
        }

        var request = new UserRequest { RawText = InputText.Trim(), UseAdvancedModel = IsAdvancedMode };
        if (string.IsNullOrWhiteSpace(request.RawText))
        {
            return;
        }

        CharacterMood = IsAdvancedMode ? "working" : "thinking";
        CharacterStateLabel = "THINKING";
        _isResponding = true;
        Messages.Add(new ChatMessage
        {
            Text = request.RawText,
            SpeakerName = "YOU",
            IsUser = true
        });
        InputText = string.Empty;

        try
        {
            var skill = _intentRouter.Route(request);
            var result = await skill.HandleAsync(request, CancellationToken.None);
            BubbleText = result.BubbleText;
            CharacterMood = string.IsNullOrWhiteSpace(result.Mood) ? "speaking" : result.Mood;
            CharacterStateLabel = CharacterMood.ToUpperInvariant();
            await DisplayAssistantResponseAsync(result.BubbleText, isProactive: false);
            await WriteLogAsync(request.RawText, result.BubbleText, isProactive: false);
            DispatchAppCommand(result.Action);
        }
        catch (Exception ex)
        {
            BubbleText = $"요청을 처리하지 못했어요: {ex.Message}";
            IsCharacterSpeaking = false;
            CharacterMood = "error";
            CharacterStateLabel = "ERROR";
            Messages.Add(new ChatMessage
            {
                Text = BubbleText,
                SpeakerName = CharacterDisplayName.ToUpperInvariant()
            });
        }
        finally
        {
            _isResponding = false;
        }
    }

    // AppCommandSkill 이 돌려준 Action 값을 실제 동작으로 연결한다.
    // 비파괴적 명령만 즉시 처리하고, 종료처럼 위험한 명령은 의도적으로 보류한다.
    private void DispatchAppCommand(string? action)
    {
        switch (action)
        {
            case AppCommandActions.OpenSettings:
                OpenSettings();
                break;
            case AppCommandActions.OpenLog:
                OpenConversationLog();
                break;
            // TODO: AppCommandActions.ExitApp 는 확인 절차를 둔 뒤 Application.Current.Shutdown() 연결.
        }
    }

    public async Task StartConversationAsync()
    {
        if (_isResponding)
        {
            return;
        }

        _isResponding = true;
        CharacterMood = IsAdvancedMode ? "working" : "thinking";
        CharacterStateLabel = IsAdvancedMode ? "WORKING" : "THINKING";

        try
        {
            var result = await _conversationService.StartConversationAsync(CancellationToken.None);
            BubbleText = result.BubbleText;
            CharacterMood = string.IsNullOrWhiteSpace(result.Mood) ? "speaking" : result.Mood;
            CharacterStateLabel = CharacterMood.ToUpperInvariant();
            await DisplayAssistantResponseAsync(result.BubbleText, isProactive: true);
            await WriteLogAsync(string.Empty, result.BubbleText, isProactive: true);
        }
        catch (Exception ex)
        {
            BubbleText = $"먼저 말을 걸지 못했어요: {ex.Message}";
            IsCharacterSpeaking = false;
            CharacterMood = "error";
            CharacterStateLabel = "ERROR";
        }
        finally
        {
            _isResponding = false;
        }
    }

    [RelayCommand]
    private void OpenSettings()
    {
        var window = new UI.Views.SettingsWindow
        {
            DataContext = _settingsViewModel,
            Owner = Application.Current.MainWindow
        };
        window.ShowDialog();
        RefreshSelectedCharacter();
        OnPropertyChanged(nameof(ActiveProviderLabel));
        ProactiveSettingsRevision++;
    }

    public void OpenConversationLog()
    {
        var window = new UI.Views.ConversationLogWindow
        {
            DataContext = new ConversationLogViewModel(_conversationLogService),
            Owner = Application.Current.MainWindow
        };
        window.Show();
    }

    private async Task DisplayAssistantResponseAsync(string response, bool isProactive)
    {
        var chunks = SplitForPacedDisplay(response);
        IsCharacterSpeaking = true;
        try
        {
            for (var index = 0; index < chunks.Count; index++)
            {
                var message = new ChatMessage
                {
                    SpeakerName = isProactive
                        ? $"{CharacterDisplayName.ToUpperInvariant()} • 먼저 말 걸기"
                        : CharacterDisplayName.ToUpperInvariant(),
                    IsProactive = isProactive && index == 0,
                    IsTyping = true
                };
                Messages.Add(message);
                await TypeMessageAsync(message, chunks[index]);

                if (index + 1 < chunks.Count)
                {
                    await Task.Delay(320);
                }
            }
        }
        finally
        {
            IsCharacterSpeaking = false;
        }

        CharacterMood = "idle";
        CharacterStateLabel = "IDLE";
    }

    private static async Task TypeMessageAsync(ChatMessage message, string text)
    {
        var elements = GetTextElements(text);
        var batchSize = elements.Count switch
        {
            > 1200 => 5,
            > 700 => 3,
            > 350 => 2,
            _ => 1
        };
        var baseDelayMilliseconds = elements.Count switch
        {
            > 1200 => 8,
            > 700 => 11,
            > 350 => 16,
            _ => 24
        };
        var builder = new StringBuilder(text.Length);

        for (var index = 0; index < elements.Count;)
        {
            var currentBatchSize = Math.Min(batchSize, elements.Count - index);
            string lastElement = string.Empty;
            for (var batchIndex = 0; batchIndex < currentBatchSize; batchIndex++)
            {
                lastElement = elements[index++];
                builder.Append(lastElement);
            }

            message.Text = builder.ToString();
            var delay = baseDelayMilliseconds + GetNaturalPauseMilliseconds(lastElement);
            await Task.Delay(delay);
        }

        message.IsTyping = false;
    }

    private static IReadOnlyList<string> GetTextElements(string text)
    {
        var elements = new List<string>();
        var enumerator = StringInfo.GetTextElementEnumerator(text);
        while (enumerator.MoveNext())
        {
            elements.Add(enumerator.GetTextElement());
        }

        return elements;
    }

    private static int GetNaturalPauseMilliseconds(string textElement)
    {
        return textElement switch
        {
            "." or "!" or "?" or "…" => 180,
            "," or ";" or ":" => 80,
            "\n" or "\r\n" => 220,
            _ => 0
        };
    }

    private static IReadOnlyList<string> SplitForPacedDisplay(string response)
    {
        const int targetLength = 130;
        const int maximumLength = 220;
        var normalized = response.Replace("\r\n", "\n").Trim();
        if (normalized.Length <= maximumLength)
        {
            return [normalized];
        }

        var segments = Regex.Split(
                normalized,
                @"(?<=\n)|(?<=[.!?…])\s+")
            .Where(segment => !string.IsNullOrWhiteSpace(segment))
            .Select(segment => segment.Trim())
            .ToArray();
        var chunks = new List<string>();
        var current = new StringBuilder();

        foreach (var segment in segments)
        {
            if (segment.Length > maximumLength)
            {
                FlushChunk(current, chunks);
                SplitLongSegment(segment, maximumLength, chunks);
                continue;
            }

            if (current.Length > 0 &&
                current.Length + 1 + segment.Length > maximumLength)
            {
                FlushChunk(current, chunks);
            }

            if (current.Length > 0)
            {
                current.Append(segment.StartsWith("- ", StringComparison.Ordinal) ||
                               segment.StartsWith("* ", StringComparison.Ordinal) ||
                               char.IsDigit(segment[0])
                    ? '\n'
                    : ' ');
            }

            current.Append(segment);
            if (current.Length >= targetLength)
            {
                FlushChunk(current, chunks);
            }
        }

        FlushChunk(current, chunks);
        return chunks.Count == 0 ? [normalized] : chunks;
    }

    private static void SplitLongSegment(string segment, int maximumLength, List<string> chunks)
    {
        var remaining = segment;
        while (remaining.Length > maximumLength)
        {
            var splitAt = remaining.LastIndexOfAny([' ', '\n', ',', ';'], maximumLength - 1, maximumLength);
            if (splitAt < maximumLength / 2)
            {
                splitAt = maximumLength;
            }

            chunks.Add(remaining[..splitAt].Trim());
            remaining = remaining[splitAt..].Trim();
        }

        if (!string.IsNullOrWhiteSpace(remaining))
        {
            chunks.Add(remaining);
        }
    }

    private static void FlushChunk(StringBuilder current, List<string> chunks)
    {
        var text = current.ToString().Trim();
        if (!string.IsNullOrWhiteSpace(text))
        {
            chunks.Add(text);
        }

        current.Clear();
    }

    private async Task WriteLogAsync(string userText, string assistantText, bool isProactive)
    {
        try
        {
            var config = _configLoader.Load();
            if (!config.App.EnableLogging)
            {
                return;
            }

            await _conversationLogService.AppendAsync(new ConversationLogEntry
            {
                Timestamp = DateTimeOffset.Now,
                UserText = userText,
                AssistantText = assistantText,
                Provider = config.Llm.Provider,
                Model = config.Llm.Model,
                IsProactive = isProactive
            }, CancellationToken.None);
        }
        catch
        {
            // Logging must never interrupt the conversation.
        }
    }

    private void RefreshSelectedCharacter()
    {
        var config = _configLoader.Load();
        var character = CharacterCatalog.Get(config.App.GhostId);
        SelectedCharacterId = character.Id;
        CharacterDisplayName = character.DisplayName;
        if (config.App.CharacterProfiles.TryGetValue(character.Id, out var profile))
        {
            CharacterScale = profile.CharacterScale <= 0
                ? 1.0
                : Math.Clamp(profile.CharacterScale, 0.55, 2.0);
            SelectedCharacterSizePresetId = character.Presentation.SizePresets.Any(preset =>
                string.Equals(preset.Id, profile.CharacterSizePresetId, StringComparison.OrdinalIgnoreCase))
                ? profile.CharacterSizePresetId.Trim()
                : character.Presentation.DefaultSizePresetId;
            SelectedCharacterFramingPresetId = character.Presentation.FramingPresets.Any(preset =>
                string.Equals(preset.Id, profile.CharacterFramingPresetId, StringComparison.OrdinalIgnoreCase))
                ? profile.CharacterFramingPresetId.Trim()
                : character.Presentation.DefaultFramingPresetId;
        }
        else
        {
            CharacterScale = 1.0;
            SelectedCharacterSizePresetId = character.Presentation.DefaultSizePresetId;
            SelectedCharacterFramingPresetId = character.Presentation.DefaultFramingPresetId;
        }

        BubbleText = $"{character.DisplayName}가 기다리고 있어요.";
    }
}
