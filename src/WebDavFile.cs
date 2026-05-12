using Nerdbank.Streams;
using WebDav;

namespace OwlCore.Storage.WebDav;

public partial class WebDavFile : IChildFile
    , ICreatedAtOffset
    , ILastAccessedAtOffset
    , ILastModifiedAtOffset
{
    internal readonly IWebDavClient _webDavClient;
    private ICreatedAtProperty? _createdAt;
    private ICreatedAtOffsetProperty? _createdAtOffset;
    private ILastAccessedAtProperty? _lastAccessedAt;
    private ILastAccessedAtOffsetProperty? _lastAccessedAtOffset;
    private ILastModifiedAtProperty? _lastModifiedAt;
    private ILastModifiedAtOffsetProperty? _lastModifiedAtOffset;

    public WebDavFile(IWebDavClient webDavClient, string path)
    {
        _webDavClient = webDavClient;
        Path = WebDavHelpers.NormalizePath(path);
    }

    public string Id => Path;

    public string Path { get; }

    public string Name => global::System.IO.Path.GetFileName(Path);

    public ICreatedAtProperty CreatedAt => _createdAt ??= new WebDavCreatedAtProperty(this, _webDavClient, Path);

    public ICreatedAtOffsetProperty CreatedAtOffset => _createdAtOffset ??= new WebDavCreatedAtOffsetProperty(this, _webDavClient, Path);

    public ILastAccessedAtProperty LastAccessedAt => _lastAccessedAt ??= new WebDavLastAccessedAtProperty(this, _webDavClient, Path);

    public ILastAccessedAtOffsetProperty LastAccessedAtOffset => _lastAccessedAtOffset ??= new WebDavLastAccessedAtOffsetProperty(this, _webDavClient, Path);

    public ILastModifiedAtProperty LastModifiedAt => _lastModifiedAt ??= new WebDavLastModifiedAtProperty(this, _webDavClient, Path);

    public ILastModifiedAtOffsetProperty LastModifiedAtOffset => _lastModifiedAtOffset ??= new WebDavLastModifiedAtOffsetProperty(this, _webDavClient, Path);

    public async Task<IFolder?> GetParentAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var parentPath = global::System.IO.Path.GetDirectoryName(Path)?.Replace('\\', '/');

        if (string.IsNullOrEmpty(parentPath))
            return null;

        var item = await _webDavClient.GetStorableFromPathAsync(parentPath, cancellationToken).ConfigureAwait(false);

        return item as IFolder;
    }

    public async Task<Stream> OpenStreamAsync(FileAccess accessMode, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        switch (accessMode)
        {
            case FileAccess.Read:
                {
                    var response = await _webDavClient.GetRawFile(Path, new GetFileParameters { CancellationToken = cancellationToken }).ConfigureAwait(false);

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
                    var readResponse = await _webDavClient.GetRawFile(Path, new GetFileParameters { CancellationToken = cancellationToken }).ConfigureAwait(false);

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
    public ValueTask DisposeAsync()
    {
        FlushToRemoteAsync().GetAwaiter().GetResult();
        base.Dispose();
        return default;
    }
#else
    public override async ValueTask DisposeAsync()
    {
        await FlushToRemoteAsync().ConfigureAwait(false);
        await base.DisposeAsync().ConfigureAwait(false);
    }
#endif

    private async Task FlushToRemoteAsync()
    {
        if (!CanRead)
            return;

        Position = 0;
        var response = await _client.PutFile(_path, this, new PutFileParameters { CancellationToken = _cancellationToken }).ConfigureAwait(false);

        if (!response.IsSuccessful)
            throw new IOException($"Failed to write file. Status code: {response.StatusCode}.");
    }
}
