using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using LivingMetalGhost.Core.Models;

namespace LivingMetalGhost.UI.Controls;

public partial class CharacterHost : UserControl
{
    private static readonly Dictionary<string, BitmapImage> ImageCache = new(StringComparer.OrdinalIgnoreCase);

    private readonly DispatcherTimer _blinkTimer;
    private readonly DispatcherTimer _speakingTimer;
    private readonly DispatcherTimer _moodCycleTimer;
    private bool _isBlinking;
    private int _speakingFrameIndex;
    private int _moodCycleFrameIndex;
    private SpriteCharacterVisualProfile? _spriteVisual;
    private ModularCharacterVisualProfile? _modularVisual;
    private readonly Dictionary<string, Image> _modularLayerImages = new(StringComparer.OrdinalIgnoreCase);

    public CharacterHost()
    {
        InitializeComponent();

        _blinkTimer = new DispatcherTimer();
        _blinkTimer.Tick += BlinkTimer_OnTick;

        _speakingTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(170)
        };
        _speakingTimer.Tick += SpeakingTimer_OnTick;

        _moodCycleTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        _moodCycleTimer.Tick += MoodCycleTimer_OnTick;

        Loaded += (_, _) => RefreshCharacter();
    }

    public static readonly DependencyProperty CharacterIdProperty = DependencyProperty.Register(
        nameof(CharacterId),
        typeof(string),
        typeof(CharacterHost),
        new PropertyMetadata("ssuang", OnCharacterChanged));

    public static readonly DependencyProperty MoodProperty = DependencyProperty.Register(
        nameof(Mood),
        typeof(string),
        typeof(CharacterHost),
        new PropertyMetadata("idle", OnCharacterChanged));

    public static readonly DependencyProperty IsSpeakingProperty = DependencyProperty.Register(
        nameof(IsSpeaking),
        typeof(bool),
        typeof(CharacterHost),
        new PropertyMetadata(false, OnCharacterChanged));

    public static readonly DependencyProperty SizePresetIdProperty = DependencyProperty.Register(
        nameof(SizePresetId),
        typeof(string),
        typeof(CharacterHost),
        new PropertyMetadata("normal", OnCharacterChanged));

    public static readonly DependencyProperty FramingPresetIdProperty = DependencyProperty.Register(
        nameof(FramingPresetId),
        typeof(string),
        typeof(CharacterHost),
        new PropertyMetadata("full-body", OnCharacterChanged));

    public static readonly DependencyProperty UserScaleProperty = DependencyProperty.Register(
        nameof(UserScale),
        typeof(double),
        typeof(CharacterHost),
        new PropertyMetadata(1.0, OnCharacterChanged));

    public string CharacterId
    {
        get => (string)GetValue(CharacterIdProperty);
        set => SetValue(CharacterIdProperty, value);
    }

    public string Mood
    {
        get => (string)GetValue(MoodProperty);
        set => SetValue(MoodProperty, value);
    }

    public bool IsSpeaking
    {
        get => (bool)GetValue(IsSpeakingProperty);
        set => SetValue(IsSpeakingProperty, value);
    }

    public string SizePresetId
    {
        get => (string)GetValue(SizePresetIdProperty);
        set => SetValue(SizePresetIdProperty, value);
    }

    public string FramingPresetId
    {
        get => (string)GetValue(FramingPresetIdProperty);
        set => SetValue(FramingPresetIdProperty, value);
    }

    public double UserScale
    {
        get => (double)GetValue(UserScaleProperty);
        set => SetValue(UserScaleProperty, value);
    }

    private static void OnCharacterChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is CharacterHost host && host.IsLoaded)
        {
            host.RefreshCharacter();
        }
    }

    private void RefreshCharacter()
    {
        var character = CharacterCatalog.Get(CharacterId);
        _spriteVisual = character.Visual as SpriteCharacterVisualProfile;
        _modularVisual = character.Visual as ModularCharacterVisualProfile;
        var usesCustomVisual = _spriteVisual is not null || _modularVisual is not null;

        SpriteCharacter.Visibility = usesCustomVisual ? Visibility.Visible : Visibility.Collapsed;

        _blinkTimer.Stop();
        _speakingTimer.Stop();
        _moodCycleTimer.Stop();
        _isBlinking = false;
        _speakingFrameIndex = 0;
        _moodCycleFrameIndex = 0;
        ResetVisualLayers();
        StopMotion();

        if (!usesCustomVisual)
        {
            return;
        }

        if (_spriteVisual is not null)
        {
            ApplyFrameLayout(_spriteVisual.Width, _spriteVisual.Height, character.Presentation);
            SpriteBaseLayer.Source = LoadBitmap(_spriteVisual.IdleSpritePath);
            StartMotion(_spriteVisual.IdleMotion);

            if (IsSpeaking)
            {
                StartMotion(_spriteVisual.SpeakingMotion ?? _spriteVisual.IdleMotion);
                ShowSpeakingFrame();
                _speakingTimer.Start();
                return;
            }

            if (_spriteVisual.MoodCycleSpritePaths.TryGetValue(Mood, out var cycleSprites) && cycleSprites.Count > 0)
            {
                ShowMoodCycleFrame(cycleSprites);
                _moodCycleTimer.Start();
                if (_spriteVisual.MoodBlinkSpritePaths.ContainsKey(Mood))
                {
                    ScheduleNextBlink();
                    _blinkTimer.Start();
                }
                return;
            }

            if (_spriteVisual.MoodSpritePaths.TryGetValue(Mood, out var poseSprite))
            {
                ShowPose(poseSprite);
                if (_spriteVisual.MoodBlinkSpritePaths.ContainsKey(Mood))
                {
                    ScheduleNextBlink();
                    _blinkTimer.Start();
                }
                return;
            }

            HidePose();
            if (!string.IsNullOrWhiteSpace(_spriteVisual.BlinkSpritePath))
            {
                ScheduleNextBlink();
                _blinkTimer.Start();
            }
            return;
        }

        if (_modularVisual is null)
        {
            return;
        }

        ApplyFrameLayout(_modularVisual.Width, _modularVisual.Height, character.Presentation);
        InitializeModularLayers(_modularVisual);
        ApplyModularState(GetBaseModularStateName());
        StartMotion(_modularVisual.IdleMotion);

        if (IsSpeaking)
        {
            StartMotion(_modularVisual.SpeakingMotion ?? _modularVisual.IdleMotion);
            ShowSpeakingFrame();
            _speakingTimer.Start();
            return;
        }

        if (_modularVisual.States.ContainsKey(_modularVisual.BlinkStateName))
        {
            ScheduleNextBlink();
            _blinkTimer.Start();
        }
    }

    private async void BlinkTimer_OnTick(object? sender, EventArgs e)
    {
        if (_isBlinking || !UsesCustomCharacter() || !CanBlink())
        {
            return;
        }

        _isBlinking = true;
        _blinkTimer.Stop();
        ShowBlinkFrame();
        await Task.Delay(130);

        if (UsesCustomCharacter() && CanBlink())
        {
            ShowIdleFrame();
            ScheduleNextBlink();
            _blinkTimer.Start();
        }

        _isBlinking = false;
    }

    private void SpeakingTimer_OnTick(object? sender, EventArgs e)
    {
        ShowSpeakingFrame();
    }

    private void MoodCycleTimer_OnTick(object? sender, EventArgs e)
    {
        if (_isBlinking || _spriteVisual is null)
        {
            return;
        }

        if (_spriteVisual.MoodCycleSpritePaths.TryGetValue(Mood, out var cycleSprites) && cycleSprites.Count > 0)
        {
            ShowMoodCycleFrame(cycleSprites);
        }
    }

    private void ShowMoodCycleFrame(IReadOnlyList<string> cycleSprites)
    {
        var path = cycleSprites[_moodCycleFrameIndex % cycleSprites.Count];
        _moodCycleFrameIndex++;
        ShowPose(path);
    }

    private void ShowSpeakingFrame()
    {
        if (_spriteVisual is not null)
        {
            if (_spriteVisual.SpeakingSpritePaths.Count == 0)
            {
                HidePose();
                return;
            }

            var spritePath = _spriteVisual.SpeakingSpritePaths[_speakingFrameIndex % _spriteVisual.SpeakingSpritePaths.Count];
            _speakingFrameIndex++;
            ShowPose(spritePath);
            return;
        }

        if (_modularVisual is null)
        {
            return;
        }

        var baseStateName = GetBaseModularStateName();
        var speakingStates = _modularVisual.SpeakingStatesByState.TryGetValue(baseStateName, out var stateFrames) &&
                             stateFrames.Count > 0
            ? stateFrames
            : _modularVisual.SpeakingStates;

        if (speakingStates.Count == 0)
        {
            ApplyModularState(baseStateName);
            return;
        }

        var overlayState = speakingStates[_speakingFrameIndex % speakingStates.Count];
        _speakingFrameIndex++;
        ApplyModularState(GetBaseModularState(), overlayState);
    }

    private void ShowPose(string spritePath)
    {
        if (_spriteVisual is null)
        {
            return;
        }

        SpritePoseLayer.Source = LoadBitmap(spritePath);
        SpritePoseLayer.Visibility = SpritePoseLayer.Source is null ? Visibility.Collapsed : Visibility.Visible;
        SpriteBaseLayer.Visibility = SpritePoseLayer.Source is null ? Visibility.Visible : Visibility.Collapsed;
    }

    private void HidePose()
    {
        SpritePoseLayer.Source = null;
        SpritePoseLayer.Visibility = Visibility.Collapsed;
        SpriteBaseLayer.Visibility = Visibility.Visible;
    }

    private void ResetVisualLayers()
    {
        ModularLayerHost.Children.Clear();
        ModularLayerHost.Visibility = Visibility.Collapsed;
        _modularLayerImages.Clear();
        SpriteBaseLayer.Source = null;
        SpriteBaseLayer.Visibility = Visibility.Collapsed;
        SpritePoseLayer.Source = null;
        SpritePoseLayer.Visibility = Visibility.Collapsed;
    }

    private bool UsesCustomCharacter()
    {
        return _spriteVisual is not null || _modularVisual is not null;
    }

    private void ApplyFrameLayout(double width, double height, CharacterPresentationProfile presentation)
    {
        var framingPreset = ResolveFramingPreset(presentation);
        var userScale = Math.Clamp(UserScale, 0.55, 2.0);
        var scaledWidth = width * userScale;
        var scaledHeight = height * userScale;
        var viewportHeight = scaledHeight / framingPreset.Zoom;
        var overflowHeight = Math.Max(0, scaledHeight - viewportHeight);

        CharacterFrame.Width = scaledWidth;
        CharacterFrame.Height = scaledHeight;
        Width = scaledWidth;
        Height = viewportHeight;

        CharacterScale.ScaleX = 1;
        CharacterScale.ScaleY = 1;
        CharacterPresentationTranslate.X = 0;
        CharacterPresentationTranslate.Y = -(overflowHeight * framingPreset.FocusY);
    }

    private CharacterFramingPreset ResolveFramingPreset(CharacterPresentationProfile presentation)
    {
        return presentation.FramingPresets.FirstOrDefault(preset =>
                   string.Equals(preset.Id, FramingPresetId, StringComparison.OrdinalIgnoreCase))
               ?? presentation.FramingPresets.First();
    }

    private void ScheduleNextBlink()
    {
        _blinkTimer.Interval = TimeSpan.FromMilliseconds(Random.Shared.Next(2800, 6200));
    }

    private void StartMotion(CharacterMotionProfile? motion)
    {
        StopMotion();
        if (motion is null)
        {
            return;
        }

        var animation = new DoubleAnimation
        {
            From = motion.FromY,
            To = motion.ToY,
            Duration = TimeSpan.FromMilliseconds(motion.DurationMilliseconds),
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever
        };
        CharacterMotionTranslate.BeginAnimation(TranslateTransform.YProperty, animation);
    }

    private void StopMotion()
    {
        CharacterMotionTranslate.BeginAnimation(TranslateTransform.YProperty, null);
    }

    private bool CanBlink()
    {
        if (_spriteVisual is not null)
        {
            if (string.Equals(Mood, "idle", StringComparison.OrdinalIgnoreCase))
            {
                return !string.IsNullOrWhiteSpace(_spriteVisual.BlinkSpritePath);
            }

            return _spriteVisual.MoodBlinkSpritePaths.ContainsKey(Mood);
        }

        return _modularVisual is not null &&
               string.Equals(Mood, "idle", StringComparison.OrdinalIgnoreCase) &&
               _modularVisual.States.ContainsKey(_modularVisual.BlinkStateName);
    }

    private void ShowBlinkFrame()
    {
        if (_spriteVisual is not null)
        {
            if (_spriteVisual.MoodBlinkSpritePaths.TryGetValue(Mood, out var moodBlinkPath) &&
                !string.IsNullOrWhiteSpace(moodBlinkPath))
            {
                ShowPose(moodBlinkPath);
                return;
            }

            if (!string.IsNullOrWhiteSpace(_spriteVisual.BlinkSpritePath))
            {
                ShowPose(_spriteVisual.BlinkSpritePath!);
            }

            return;
        }

        if (_modularVisual is not null)
        {
            ApplyModularState(GetBaseModularState(), GetNamedModularState(_modularVisual.BlinkStateName));
        }
    }

    private void ShowIdleFrame()
    {
        if (_spriteVisual is not null)
        {
            if (_spriteVisual.MoodCycleSpritePaths.TryGetValue(Mood, out var cycleSprites) && cycleSprites.Count > 0)
            {
                var index = Math.Max(0, _moodCycleFrameIndex - 1) % cycleSprites.Count;
                ShowPose(cycleSprites[index]);
                return;
            }

            if (_spriteVisual.MoodSpritePaths.TryGetValue(Mood, out var moodSprite))
            {
                ShowPose(moodSprite);
                return;
            }

            HidePose();
            return;
        }

        if (_modularVisual is not null)
        {
            ApplyModularState(GetBaseModularStateName());
        }
    }

    private void InitializeModularLayers(ModularCharacterVisualProfile visual)
    {
        ModularLayerHost.Children.Clear();
        _modularLayerImages.Clear();

        foreach (var layerName in visual.LayerOrder)
        {
            var image = new Image
            {
                Stretch = Stretch.Uniform,
                VerticalAlignment = VerticalAlignment.Bottom,
                Visibility = Visibility.Collapsed
            };

            _modularLayerImages[layerName] = image;
            ModularLayerHost.Children.Add(image);
        }

        ModularLayerHost.Visibility = Visibility.Visible;
        SpriteBaseLayer.Visibility = Visibility.Collapsed;
        SpritePoseLayer.Visibility = Visibility.Collapsed;
    }

    private void ApplyModularState(string stateName)
    {
        if (_modularVisual is null)
        {
            return;
        }

        ApplyModularState(GetNamedModularState(stateName));
    }

    private void ApplyModularState(ModularCharacterState? state)
    {
        ApplyModularState(state, null);
    }

    private void ApplyModularState(ModularCharacterState? baseState, ModularCharacterState? overlayState)
    {
        if (_modularVisual is null)
        {
            return;
        }

        foreach (var layerName in _modularVisual.LayerOrder)
        {
            string? path = null;
            if (overlayState?.LayerPaths.TryGetValue(layerName, out var overlayPath) == true)
            {
                path = overlayPath;
            }
            else if (baseState?.LayerPaths.TryGetValue(layerName, out var overridePath) == true)
            {
                path = overridePath;
            }
            else if (_modularVisual.DefaultLayerPaths.TryGetValue(layerName, out var defaultPath))
            {
                path = defaultPath;
            }

            if (!_modularLayerImages.TryGetValue(layerName, out var image))
            {
                continue;
            }

            image.Source = LoadBitmap(path);
            image.Visibility = image.Source is null ? Visibility.Collapsed : Visibility.Visible;
        }
    }

    private string GetBaseModularStateName()
    {
        if (_modularVisual is null)
        {
            return "idle";
        }

        return string.IsNullOrWhiteSpace(Mood) ||
               string.Equals(Mood, "speaking", StringComparison.OrdinalIgnoreCase) ||
               !_modularVisual.States.ContainsKey(Mood)
            ? _modularVisual.IdleStateName
            : Mood;
    }

    private ModularCharacterState? GetBaseModularState()
    {
        return GetNamedModularState(GetBaseModularStateName());
    }

    private ModularCharacterState? GetNamedModularState(string stateName)
    {
        if (_modularVisual is null)
        {
            return null;
        }

        _modularVisual.States.TryGetValue(stateName, out var state);
        return state;
    }

    private static BitmapImage? LoadBitmap(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return null;
        }

        if (ImageCache.TryGetValue(path, out var cached))
        {
            return cached;
        }

        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(path, UriKind.Absolute);
            bitmap.EndInit();
            bitmap.Freeze();
            ImageCache[path] = bitmap;
            return bitmap;
        }
        catch (Exception ex)
        {
            App.WriteCrashLog("sprite-load", ex);
            return null;
        }
    }
}
