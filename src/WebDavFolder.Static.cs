using WebDav;

namespace OwlCore.Storage.WebDav;

public partial class WebDavFolder
{
    public static async Task<WebDavFolder> GetFromWebDavPathAsync(IWebDavClient webDavClient, string path, CancellationToken cancellationToken = default)
    {
        var folder = await TryGetFromWebDavPathAsync(webDavClient, path, cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();

        return folder is null
            ? throw new FileNotFoundException($"Cannot find folder in WebDAV server with path \"{path}\".")
            : folder;
    }

    public static async Task<WebDavFolder?> TryGetFromWebDavPathAsync(IWebDavClient webDavClient, string path, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        foreach (var c in global::System.IO.Path.GetInvalidPathChars())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (path.Contains(c))
                throw new FormatException($"Provided path contains invalid character '{c}'.");
        }

        var item = await webDavClient.GetStorableFromPathAsync(path, cancellationToken);

        return item as WebDavFolder;
    }
}
