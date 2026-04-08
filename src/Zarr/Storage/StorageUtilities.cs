namespace Zarr.Storage;

public static class StorageUtilities
{
    public static string NormalizePath(string? path) => PathUtilities.Normalize(path);

    public static string CombinePath(string? left, string? right) => PathUtilities.Combine(left, right);

    public static bool IsRemoteUri(string? value)
    {
        return TryParseAbsoluteUri(value, out var uri) && !uri.IsFile;
    }

    internal static bool TryParseAbsoluteUri(string? value, out Uri uri)
    {
        if (!string.IsNullOrWhiteSpace(value) &&
            Uri.TryCreate(value, UriKind.Absolute, out uri))
        {
            return true;
        }

        uri = null!;
        return false;
    }
}
