using Nerdbank.Streams;
using WebDav;

namespace OwlCore.Storage.WebDav;

public partial class WebDavFile : IChildFile
{
    internal readonly IWebDavClient _webDavClient;

    public WebDavFile(IWebDavClient webDavClient, string path)
    {
        _webDavClient = webDavClient;
        Path = WebDavHelpers.NormalizePath(path);
    }

    public string Id => Path;

    public string Path { get; }

    public string Name => global::System.IO.Path.GetFileName(Path);

    public async Task<IFolder?> GetParentAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var parentPath = global::System.IO.Path.GetDirectoryName(Path)?.Replace('\\', '/');

        if (string.IsNullOrEmpty(parentPath))
            return null;

        var item = await _webDavClient.GetStorableFromPathAsync(parentPath, cancellationToken);

        return item as IFolder;
    }

    public async Task<Stream> OpenStreamAsync(FileAccess accessMode, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        switch (accessMode)
        {
            case FileAccess.Read:
                {
                    var response = await _webDavClient.GetRawFile(Path, new GetFileParameters { CancellationToken = cancellationToken });

                    if (!response.IsSuccessful)
                    {
                        response.Dispose();
                        throw new IOException($"Failed to open file for reading. Status code: {response.StatusCode}.");
                    }

                    return response.Stream;
                }
            case FileAccess.Write:
                return new WebDavWriteBackStream(_webDavClient, Path, cancellationToken);
            case FileAccess.ReadWrite:
                {
                    var readResponse = await _webDavClient.GetRawFile(Path, new GetFileParameters { CancellationToken = cancellationToken });

                    if (!readResponse.IsSuccessful)
                    {
                        readResponse.Dispose();
                        throw new IOException($"Failed to open file for reading. Status code: {readResponse.StatusCode}.");
                    }

                    var writeStream = new WebDavWriteBackStream(_webDavClient, Path, cancellationToken);
                    return FullDuplexStream.Splice(readResponse.Stream, writeStream);
                }
            default:
                throw new ArgumentOutOfRangeException(nameof(accessMode));
        }
    }
}

internal sealed class WebDavWriteBackStream : MemoryStream
{
    private readonly IWebDavClient _client;
    private readonly string _path;
    private readonly CancellationToken _cancellationToken;

    public WebDavWriteBackStream(IWebDavClient client, string path, CancellationToken cancellationToken)
    {
        _client = client;
        _path = path;
        _cancellationToken = cancellationToken;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            FlushToRemoteAsync().GetAwaiter().GetResult();

        base.Dispose(disposing);
    }

#if NETSTANDARD2_0
    public new ValueTask DisposeAsync()
    {
        FlushToRemoteAsync().GetAwaiter().GetResult();
        base.Dispose();
        return default;
    }
#else
    public override async ValueTask DisposeAsync()
    {
        await FlushToRemoteAsync();
        await base.DisposeAsync();
    }
#endif

    private async Task FlushToRemoteAsync()
    {
        if (!CanRead)
            return;

        Position = 0;
        var response = await _client.PutFile(_path, this, new PutFileParameters { CancellationToken = _cancellationToken });

        if (!response.IsSuccessful)
            throw new IOException($"Failed to write file. Status code: {response.StatusCode}.");
    }
}
