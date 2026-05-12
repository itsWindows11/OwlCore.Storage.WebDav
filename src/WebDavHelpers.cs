using WebDav;

namespace OwlCore.Storage.WebDav;

internal static class WebDavHelpers
{
    internal static string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return "/";

        var normalized = path.Replace('\\', '/');

        if (!normalized.StartsWith("/", StringComparison.Ordinal))
            normalized = "/" + normalized;

        while (normalized.Contains("//"))
            normalized = normalized.Replace("//", "/");

        if (normalized.Length > 1 && normalized.EndsWith("/", StringComparison.Ordinal))
            normalized = normalized.TrimEnd('/');

        return normalized;
    }

    internal static string CombinePath(string parent, string name)
    {
        var normalizedParent = NormalizePath(parent);
        var sanitizedName = name.Replace('\\', '/').Trim('/');

        if (string.IsNullOrWhiteSpace(sanitizedName))
            throw new ArgumentException("Name cannot be empty.", nameof(name));

        return normalizedParent == "/"
            ? "/" + sanitizedName
            : normalizedParent + "/" + sanitizedName;
    }

    internal static async Task<IStorable?> GetStorableFromPathAsync(this IWebDavClient webDavClient, string path, CancellationToken cancellationToken = default)
    {
        path = NormalizePath(path);

        if (path == "/")
            return new WebDavFolder(webDavClient, "/");

        var response = await webDavClient.Propfind(path, new PropfindParameters
        {
            ApplyTo = ApplyTo.Propfind.ResourceOnly,
            CancellationToken = cancellationToken
        });

        if (!response.IsSuccessful || response.Resources.Count == 0)
            return null;

        var match = response.Resources
            .FirstOrDefault(x => NormalizePath(UriToPath(x.Uri)) == path)
            ?? response.Resources.First();

        return match.IsCollection
            ? new WebDavFolder(webDavClient, NormalizePath(UriToPath(match.Uri)))
            : new WebDavFile(webDavClient, NormalizePath(UriToPath(match.Uri)));
    }

    internal static string UriToPath(string? uri)
    {
        if (string.IsNullOrWhiteSpace(uri))
            return "/";

        if (Uri.TryCreate(uri, UriKind.Absolute, out var absolute))
            return NormalizePath(Uri.UnescapeDataString(absolute.AbsolutePath));

        return NormalizePath(Uri.UnescapeDataString(uri));
    }
}
