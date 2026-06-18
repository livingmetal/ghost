using CommunityToolkit.Mvvm.ComponentModel;

namespace LivingMetalGhost.Core.Models;

public sealed class ChatMessage : ObservableObject
{
    private string _text = string.Empty;
    private bool _isTyping;
    private bool _isAdvanced;
    private bool _isHiddenInAdvanced;
    private string _immediateText = string.Empty;

    public string Text
    {
        get => _text;
        set
        {
            if (SetProperty(ref _text, value))
            {
                OnPropertyChanged(nameof(DisplayText));
                OnPropertyChanged(nameof(AdvancedDisplayText));
            }
        }
    }

    public string DisplayText => IsTyping ? $"{Text}▌" : Text;

    public bool IsTyping
    {
        get => _isTyping;
        set
        {
            if (SetProperty(ref _isTyping, value))
            {
                OnPropertyChanged(nameof(DisplayText));
                OnPropertyChanged(nameof(AdvancedDisplayText));
            }
        }
    }

    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.Now;
    public string SpeakerName { get; init; } = "ASSISTANT";
    public bool IsUser { get; init; }
    public bool IsProactive { get; init; }

    /// <summary>고급 모드 답변이면 말풍선/타이핑/청크 연출을 피하고 GPT식 본문으로 렌더링한다.</summary>
    public bool IsAdvanced
    {
        get => _isAdvanced;
        set
        {
            if (SetProperty(ref _isAdvanced, value))
            {
                OnPropertyChanged(nameof(AdvancedDisplayText));
            }
        }
    }

    /// <summary>고급 모드에서 청크로 추가된 중복 assistant 메시지를 화면에서 숨긴다.</summary>
    public bool IsHiddenInAdvanced
    {
        get => _isHiddenInAdvanced;
        set => SetProperty(ref _isHiddenInAdvanced, value);
    }

    /// <summary>고급 모드에서 TypeMessageAsync가 돌더라도 즉시 완성본을 보여주기 위한 원문.</summary>
    public string ImmediateText
    {
        get => _immediateText;
        set
        {
            if (SetProperty(ref _immediateText, value ?? string.Empty))
            {
                OnPropertyChanged(nameof(AdvancedDisplayText));
            }
        }
    }

    public string AdvancedDisplayText =>
        IsAdvanced && !string.IsNullOrEmpty(ImmediateText)
            ? ImmediateText
            : Text;

    /// <summary>롤플레잉 메시지면 (속마음) 괄호도 이탤릭으로 렌더링한다.</summary>
    public bool IsRoleplay { get; init; }
}
