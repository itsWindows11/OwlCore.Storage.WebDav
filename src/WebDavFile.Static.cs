using WebDav;

namespace OwlCore.Storage.WebDav;

public partial class WebDavFile
{
    public static async Task<WebDavFile> GetFromWebDavPathAsync(IWebDavClient webDavClient, string path, CancellationToken cancellationToken = default)
    {
        var file = await TryGetFromWebDavPathAsync(webDavClient, path, cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();

        return file is null
            ? throw new FileNotFoundException($"Cannot find file in WebDAV server with path \"{path}\".")
            : file;
    }

    public static async Task<WebDavFile?> TryGetFromWebDavPathAsync(IWebDavClient webDavClient, string path, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        foreach (var c in global::System.IO.Path.GetInvalidPathChars())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (path.Contains(c))
                throw new FormatException($"Provided path contains invalid character '{c}'.");
        }

        var item = await webDavClient.GetStorableFromPathAsync(path, cancellationToken);

        return item as WebDavFile;
    }
}
