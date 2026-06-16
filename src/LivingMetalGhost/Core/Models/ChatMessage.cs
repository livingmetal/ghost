using CommunityToolkit.Mvvm.ComponentModel;

namespace LivingMetalGhost.Core.Models;

public sealed class ChatMessage : ObservableObject
{
    private string _text = string.Empty;
    private bool _isTyping;

    public string Text
    {
        get => _text;
        set
        {
            if (SetProperty(ref _text, value))
            {
                OnPropertyChanged(nameof(DisplayText));
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
            }
        }
    }

    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.Now;
    public string SpeakerName { get; init; } = "ASSISTANT";
    public bool IsUser { get; init; }
    public bool IsProactive { get; init; }

    /// <summary>롤플레잉 메시지면 (속마음) 괄호도 이탤릭으로 렌더링한다.</summary>
    public bool IsRoleplay { get; init; }
}
