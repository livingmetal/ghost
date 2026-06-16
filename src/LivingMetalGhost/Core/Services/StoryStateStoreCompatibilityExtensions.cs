using System.IO;
using System.Text.Json;
using LivingMetalGhost.Core.Config;
using LivingMetalGhost.Core.Models;

namespace LivingMetalGhost.Core.Services;

public static class StoryStateStoreCompatibilityExtensions
{
    public static StoryState SetEnabled(this StoryStateStore store, bool enabled) =>
        store.SetEnabled(enabled, ResolveCurrentStoryTemplate());

    public static StoryState Reset(this StoryStateStore store, bool keepEnabled) =>
        store.Reset(keepEnabled, ResolveCurrentStoryTemplate());

    private static string ResolveCurrentStoryTemplate()
    {
        try
        {
            var root = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "LivingMetalGhost");
            var configPath = Path.Combine(root, "config.json");
            if (!File.Exists(configPath))
            {
                return string.Empty;
            }

            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            };
            var config = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(configPath), options) ?? new AppConfig();
            config.App.CharacterProfiles ??= [];
            var character = CharacterCatalog.Get(config.App.GhostId);
            if (config.App.CharacterProfiles.TryGetValue(character.Id, out var profile) &&
                !string.IsNullOrWhiteSpace(profile.StoryTemplate))
            {
                return profile.StoryTemplate;
            }

            return character.DefaultStoryTemplate;
        }
        catch
        {
            return string.Empty;
        }
    }
}
