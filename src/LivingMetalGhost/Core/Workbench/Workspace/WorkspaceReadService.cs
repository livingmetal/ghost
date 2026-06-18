using System.IO;
using LivingMetalGhost.Agents;

namespace LivingMetalGhost.Core.Workspace;

public sealed record WorkspaceFileEntry(string RelativePath, long SizeBytes);

public sealed record FileContentMatch(string RelativePath, int LineNumber, string Line);

public sealed record FileSlice(string RelativePath, int StartLine, IReadOnlyList<string> Lines);

/// <summary>
/// 고급 모드 저장소 인텔리전스의 읽기 전용 토대(coding-agent 로드맵 M2).
/// 워크스페이스 루트 안에서만 파일 목록/이름검색/내용검색/범위읽기를 제공한다.
/// 쓰기·실행·삭제는 일절 하지 않는다. 빌드 산출물과 바이너리는 제외하고 결과는 항상 상한을 둔다.
/// </summary>
public sealed class WorkspaceReadService
{
    private const long MaxSearchableFileBytes = 1_000_000;
    private const int MaxReadLines = 600;

    private static readonly HashSet<string> IgnoredDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        "bin", "obj", ".git", ".vs", ".idea", "node_modules", "packages",
        "dist", "tmp", ".ghost-work", "References"
    };

    private static readonly HashSet<string> BinaryExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".ico", ".webp",
        ".dll", ".exe", ".pdb", ".bin", ".so", ".dylib", ".lib",
        ".zip", ".7z", ".gz", ".rar", ".nupkg",
        ".mp3", ".wav", ".ogg", ".mp4", ".mov",
        ".ttf", ".otf", ".woff", ".woff2", ".pdf"
    };

    public IReadOnlyList<WorkspaceFileEntry> BuildFileMap(string root, int maxFiles = 2000)
    {
        var resolved = ResolveRoot(root);
        if (resolved is null)
        {
            return [];
        }

        var entries = new List<WorkspaceFileEntry>();
        foreach (var path in EnumerateTextFiles(resolved))
        {
            entries.Add(new WorkspaceFileEntry(ToRelative(resolved, path), SafeLength(path)));
            if (entries.Count >= maxFiles)
            {
                break;
            }
        }

        return entries
            .OrderBy(entry => entry.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public IReadOnlyList<string> SearchByName(string root, string query, int max = 50)
    {
        var resolved = ResolveRoot(root);
        if (resolved is null || string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        var results = new List<string>();
        foreach (var path in EnumerateTextFiles(resolved))
        {
            if (Path.GetFileName(path).Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                results.Add(ToRelative(resolved, path));
                if (results.Count >= max)
                {
                    break;
                }
            }
        }

        return results;
    }

    public IReadOnlyList<FileContentMatch> SearchInFiles(string root, string query, int maxResults = 50)
    {
        var resolved = ResolveRoot(root);
        if (resolved is null || string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        var matches = new List<FileContentMatch>();
        foreach (var path in EnumerateTextFiles(resolved))
        {
            if (SafeLength(path) > MaxSearchableFileBytes)
            {
                continue;
            }

            string[] lines;
            try
            {
                lines = File.ReadAllLines(path);
            }
            catch (IOException)
            {
                continue;
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }

            var relative = ToRelative(resolved, path);
            for (var i = 0; i < lines.Length; i++)
            {
                if (lines[i].Contains(query, StringComparison.OrdinalIgnoreCase))
                {
                    matches.Add(new FileContentMatch(relative, i + 1, lines[i].Trim()));
                    if (matches.Count >= maxResults)
                    {
                        return matches;
                    }
                }
            }
        }

        return matches;
    }

    public FileSlice? ReadFileSlice(string root, string relativePath, int startLine = 1, int maxLines = MaxReadLines)
    {
        var resolved = ResolveRoot(root);
        if (resolved is null || string.IsNullOrWhiteSpace(relativePath))
        {
            return null;
        }

        var fullPath = Path.GetFullPath(Path.Combine(resolved, relativePath));
        if (!WorkspaceGuard.IsInsideRoot(resolved, fullPath) || !File.Exists(fullPath))
        {
            return null;
        }

        if (BinaryExtensions.Contains(Path.GetExtension(fullPath)) || SafeLength(fullPath) > MaxSearchableFileBytes)
        {
            return null;
        }

        string[] lines;
        try
        {
            lines = File.ReadAllLines(fullPath);
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }

        var start = Math.Max(1, startLine);
        var take = Math.Clamp(maxLines, 1, MaxReadLines);
        var slice = lines.Skip(start - 1).Take(take).ToArray();
        return new FileSlice(ToRelative(resolved, fullPath), start, slice);
    }

    /// <summary>diff 등에 쓰기 위한 전체 텍스트 읽기. 루트 밖/바이너리/과대 파일은 null. 없는 파일도 null.</summary>
    public string? ReadAllText(string root, string relativePath)
    {
        var resolved = ResolveRoot(root);
        if (resolved is null || string.IsNullOrWhiteSpace(relativePath))
        {
            return null;
        }

        var fullPath = Path.GetFullPath(Path.Combine(resolved, relativePath));
        if (!WorkspaceGuard.IsInsideRoot(resolved, fullPath) ||
            !File.Exists(fullPath) ||
            BinaryExtensions.Contains(Path.GetExtension(fullPath)) ||
            SafeLength(fullPath) > MaxSearchableFileBytes)
        {
            return null;
        }

        try
        {
            return File.ReadAllText(fullPath);
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static string? ResolveRoot(string root)
    {
        return WorkspaceGuard.TryResolveRoot(root, out var resolved, out _) ? resolved : null;
    }

    private static IEnumerable<string> EnumerateTextFiles(string root)
    {
        var pending = new Stack<string>();
        pending.Push(root);

        while (pending.Count > 0)
        {
            var current = pending.Pop();

            string[] subdirectories;
            try
            {
                subdirectories = Directory.GetDirectories(current);
            }
            catch (IOException)
            {
                continue;
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }

            foreach (var directory in subdirectories)
            {
                var name = Path.GetFileName(directory);
                if (!IgnoredDirectories.Contains(name) && !name.StartsWith('_'))
                {
                    pending.Push(directory);
                }
            }

            string[] files;
            try
            {
                files = Directory.GetFiles(current);
            }
            catch (IOException)
            {
                continue;
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }

            foreach (var file in files)
            {
                if (!BinaryExtensions.Contains(Path.GetExtension(file)))
                {
                    yield return file;
                }
            }
        }
    }

    private static string ToRelative(string root, string fullPath)
    {
        return Path.GetRelativePath(root, fullPath).Replace('\\', '/');
    }

    private static long SafeLength(string path)
    {
        try
        {
            return new FileInfo(path).Length;
        }
        catch
        {
            return 0;
        }
    }
}
