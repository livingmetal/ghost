using System.Text.Json;
using LivingMetalGhost.Core.Models;

namespace LivingMetalGhost.Core.Services;

public static class RoleplayDirectorUpdateParser
{
    public static RoleplayDirectorUpdate? Parse(string? text)
    {
        var json = RoleplayJson.ExtractObject(text);
        if (json is null)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<RoleplayDirectorUpdate>(json, RoleplayJson.Options);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
