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
}
