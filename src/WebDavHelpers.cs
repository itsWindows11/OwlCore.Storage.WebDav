using System.Globalization;
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

        var resource = await webDavClient.GetResourceFromPathAsync(path, cancellationToken).ConfigureAwait(false);

        if (resource is null)
            return null;

        var matchPath = NormalizePath(UriToPath(resource.Uri));

        return resource.IsCollection
            ? new WebDavFolder(webDavClient, matchPath)
            : new WebDavFile(webDavClient, matchPath);
    }

    internal static async Task<WebDavResource?> GetResourceFromPathAsync(this IWebDavClient webDavClient, string path, CancellationToken cancellationToken = default)
    {
        path = NormalizePath(path);

        var response = await webDavClient.Propfind(path, new PropfindParameters
        {
            ApplyTo = ApplyTo.Propfind.ResourceOnly,
            CancellationToken = cancellationToken
        }).ConfigureAwait(false);

        if (!response.IsSuccessful || response.Resources.Count == 0)
            return null;

        return response.Resources
            .FirstOrDefault(x => NormalizePath(UriToPath(x.Uri)) == path)
            ?? response.Resources.First();
    }

    internal static string UriToPath(string? uri)
    {
        if (string.IsNullOrWhiteSpace(uri))
            return "/";

        if (Uri.TryCreate(uri, UriKind.Absolute, out var absolute))
            return NormalizePath(Uri.UnescapeDataString(absolute.AbsolutePath));

        return NormalizePath(Uri.UnescapeDataString(uri));
    }

    internal static DateTime? NormalizeDateTime(DateTime? value)
    {
        if (!value.HasValue || value.Value == DateTime.MinValue)
            return null;

        return value.Value.Kind switch
        {
            DateTimeKind.Utc => value.Value.ToLocalTime(),
            DateTimeKind.Unspecified => DateTime.SpecifyKind(value.Value, DateTimeKind.Local),
            _ => value
        };
    }

    internal static DateTimeOffset? NormalizeDateTimeOffset(DateTime? value)
    {
        var normalized = NormalizeDateTime(value);

        if (!normalized.HasValue)
            return null;

        return new DateTimeOffset(normalized.Value);
    }

    internal static DateTimeOffset? GetLastAccessedAtOffset(WebDavResource? resource)
    {
        var value = resource?.Properties?
            .FirstOrDefault(x => x.Name.LocalName.Equals("getlastaccessed", StringComparison.OrdinalIgnoreCase))
            ?.Value;

        if (string.IsNullOrWhiteSpace(value))
            return null;

        if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeUniversal, out var dto))
            return dto.ToLocalTime();

        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeUniversal, out var dt))
            return NormalizeDateTimeOffset(dt);

        return null;
    }
}
