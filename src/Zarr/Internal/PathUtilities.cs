namespace Zarr.Internal;

internal static class PathUtilities
{
    public static string Normalize(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        var normalized = path.Replace('\\', '/').Trim('/');
        while (normalized.Contains("//", StringComparison.Ordinal))
        {
            normalized = normalized.Replace("//", "/", StringComparison.Ordinal);
        }

        return normalized;
    }

    public static string Combine(string? left, string? right)
    {
        var lhs = Normalize(left);
        var rhs = Normalize(right);

        if (lhs.Length == 0)
        {
            return rhs;
        }

        if (rhs.Length == 0)
        {
            return lhs;
        }

        return $"{lhs}/{rhs}";
    }

    public static string Basename(string path)
    {
        var normalized = Normalize(path);
        if (normalized.Length == 0)
        {
            return string.Empty;
        }

        var segments = normalized.Split('/');
        return segments[^1];
    }

    public static IReadOnlyList<string> EnumerateParentPaths(string path)
    {
        var normalized = Normalize(path);
        if (normalized.Length == 0)
        {
            return Array.Empty<string>();
        }

        var segments = normalized.Split('/');
        var parents = new List<string> { string.Empty };
        var current = string.Empty;

        for (var index = 0; index < segments.Length - 1; index++)
        {
            current = Combine(current, segments[index]);
            parents.Add(current);
        }

        return parents;
    }
}
