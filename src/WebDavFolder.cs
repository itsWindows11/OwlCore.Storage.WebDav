using System.Runtime.CompilerServices;
using WebDav;

namespace OwlCore.Storage.WebDav;

public partial class WebDavFolder :
    IModifiableFolder,
    IChildFolder,
    IGetItem,
    IGetFirstByName,
    IGetItemRecursive,
    ICreateRenamedCopyOf,
    IMoveRenamedFrom,
    ICreatedAtOffset,
    ILastAccessedAtOffset,
    ILastModifiedAtOffset
{
    internal readonly IWebDavClient _webDavClient;
    private ICreatedAtProperty? _createdAt;
    private ICreatedAtOffsetProperty? _createdAtOffset;
    private ILastAccessedAtProperty? _lastAccessedAt;
    private ILastAccessedAtOffsetProperty? _lastAccessedAtOffset;
    private ILastModifiedAtProperty? _lastModifiedAt;
    private ILastModifiedAtOffsetProperty? _lastModifiedAtOffset;

    public WebDavFolder(IWebDavClient webDavClient, string path)
    {
        _webDavClient = webDavClient;
        Path = WebDavHelpers.NormalizePath(path);
    }

    public string Name => Path == "/" ? string.Empty : global::System.IO.Path.GetFileName(Path);

    public string Id => Path;

    public string Path { get; }

    public ICreatedAtProperty CreatedAt => _createdAt ??= new WebDavCreatedAtProperty(this, _webDavClient, Path);

    public ICreatedAtOffsetProperty CreatedAtOffset => _createdAtOffset ??= new WebDavCreatedAtOffsetProperty(this, _webDavClient, Path);

    public ILastAccessedAtProperty LastAccessedAt => _lastAccessedAt ??= new WebDavLastAccessedAtProperty(this, _webDavClient, Path);

    public ILastAccessedAtOffsetProperty LastAccessedAtOffset => _lastAccessedAtOffset ??= new WebDavLastAccessedAtOffsetProperty(this, _webDavClient, Path);

    public ILastModifiedAtProperty LastModifiedAt => _lastModifiedAt ??= new WebDavLastModifiedAtProperty(this, _webDavClient, Path);

    public ILastModifiedAtOffsetProperty LastModifiedAtOffset => _lastModifiedAtOffset ??= new WebDavLastModifiedAtOffsetProperty(this, _webDavClient, Path);

    public Task<IChildFile> CreateCopyOfAsync(IFile fileToCopy, bool overwrite, CancellationToken cancellationToken, CreateCopyOfDelegate fallback)
    {
        return CreateCopyOfAsync(fileToCopy, overwrite, fileToCopy.Name, cancellationToken,
            (modifiableFolder, file, overwriteValue, _, ct) => fallback(modifiableFolder, file, overwriteValue, ct));
    }

    public async Task<IChildFile> CreateCopyOfAsync(IFile fileToCopy, bool overwrite, string newName, CancellationToken cancellationToken, CreateRenamedCopyOfDelegate fallback)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var targetPath = WebDavHelpers.CombinePath(Path, newName);

        if (fileToCopy is WebDavFile webDavFile && ReferenceEquals(webDavFile._webDavClient, _webDavClient))
        {
            var response = await _webDavClient.Copy(webDavFile.Path, targetPath, new CopyParameters
            {
                Overwrite = overwrite,
                CancellationToken = cancellationToken
            }).ConfigureAwait(false);

            if (response.IsSuccessful)
            {
                return await WebDavFile.GetFromWebDavPathAsync(_webDavClient, targetPath, cancellationToken).ConfigureAwait(false);
            }
        }

        return await CreateCopyOfInteroperableAsync(fileToCopy, overwrite, newName, fallback, cancellationToken).ConfigureAwait(false);
    }

    public Task<IChildFile> MoveFromAsync(IChildFile fileToMove, IModifiableFolder source, bool overwrite, CancellationToken cancellationToken, MoveFromDelegate fallback)
    {
        return MoveFromAsync(fileToMove, source, overwrite, fileToMove.Name, cancellationToken,
            (modifiableFolder, file, sourceFolder, overwriteValue, _, ct) => fallback(modifiableFolder, file, sourceFolder, overwriteValue, ct));
    }

    public async Task<IChildFile> MoveFromAsync(IChildFile fileToMove, IModifiableFolder source, bool overwrite, string newName, CancellationToken cancellationToken, MoveRenamedFromDelegate fallback)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var targetPath = WebDavHelpers.CombinePath(Path, newName);

        if (fileToMove is WebDavFile webDavFile && source is WebDavFolder sourceFolder &&
            ReferenceEquals(webDavFile._webDavClient, _webDavClient) &&
            ReferenceEquals(sourceFolder._webDavClient, _webDavClient))
        {
            var response = await _webDavClient.Move(webDavFile.Path, targetPath, new MoveParameters
            {
                Overwrite = overwrite,
                CancellationToken = cancellationToken
            }).ConfigureAwait(false);

            if (response.IsSuccessful)
                return await WebDavFile.GetFromWebDavPathAsync(_webDavClient, targetPath, cancellationToken).ConfigureAwait(false);
        }

        return await MoveFromInteroperableAsync(source, fileToMove, overwrite, newName, fallback, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IChildFile> CreateFileAsync(string name, bool overwrite = false, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var filePath = WebDavHelpers.CombinePath(Path, name);

        if (!overwrite && await _webDavClient.GetStorableFromPathAsync(filePath, cancellationToken).ConfigureAwait(false) is not null)
            throw new FileAlreadyExistsException("Destination file already exists.");

        using var empty = new MemoryStream(Array.Empty<byte>());
        var response = await _webDavClient.PutFile(filePath, empty, new PutFileParameters { CancellationToken = cancellationToken }).ConfigureAwait(false);

        if (!response.IsSuccessful)
            throw new IOException($"Failed to create file \"{filePath}\". Status code: {response.StatusCode}.");

        return await WebDavFile.GetFromWebDavPathAsync(_webDavClient, filePath, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IChildFolder> CreateFolderAsync(string name, bool overwrite = false, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var folderPath = WebDavHelpers.CombinePath(Path, name);
        var existing = await _webDavClient.GetStorableFromPathAsync(folderPath, cancellationToken).ConfigureAwait(false);

        if (existing is IChildFolder existingFolder)
        {
            if (!overwrite)
                return existingFolder;

            await DeleteAsync(existingFolder, cancellationToken).ConfigureAwait(false);
        }

        var response = await _webDavClient.Mkcol(folderPath, new MkColParameters { CancellationToken = cancellationToken }).ConfigureAwait(false);

        if (!response.IsSuccessful)
            throw new IOException($"Failed to create folder \"{folderPath}\". Status code: {response.StatusCode}.");

        return await WebDavFolder.GetFromWebDavPathAsync(_webDavClient, folderPath, cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteAsync(IStorableChild item, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var response = await _webDavClient.Delete(item.Id, new DeleteParameters { CancellationToken = cancellationToken }).ConfigureAwait(false);

        if (!response.IsSuccessful)
            throw new IOException($"Failed to delete item \"{item.Id}\". Status code: {response.StatusCode}.");
    }

    public Task<IStorableChild> GetFirstByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        return GetItemAsync(WebDavHelpers.CombinePath(Path, name), cancellationToken);
    }

    public Task<IFolderWatcher> GetFolderWatcherAsync(CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("Cannot create a watcher for WebDAV folders.");
    }

    public async Task<IStorableChild> GetItemAsync(string id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var normalizedId = WebDavHelpers.NormalizePath(id);

        if (!normalizedId.StartsWith(Path == "/" ? "/" : Path + "/", StringComparison.Ordinal) && normalizedId != Path)
            throw new FileNotFoundException("The provided Id does not belong to an item in this folder.");

        var item = await _webDavClient.GetStorableFromPathAsync(normalizedId, cancellationToken).ConfigureAwait(false)
            ?? throw new FileNotFoundException($"Could not find item with path \"{id}\".");

        return item as IStorableChild ?? throw new FileNotFoundException($"Could not find item with path \"{id}\".");
    }

    public Task<IStorableChild> GetItemRecursiveAsync(string id, CancellationToken cancellationToken = default)
    {
        return GetItemAsync(id, cancellationToken);
    }

    public async IAsyncEnumerable<IStorableChild> GetItemsAsync(StorableType type = StorableType.All, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (type == StorableType.None)
            throw new ArgumentOutOfRangeException(nameof(type), $"{nameof(StorableType)}.{type} is not valid here.");

        var response = await _webDavClient.Propfind(Path, new PropfindParameters
        {
            ApplyTo = ApplyTo.Propfind.ResourceAndChildren,
            CancellationToken = cancellationToken
        }).ConfigureAwait(false);

        if (!response.IsSuccessful)
            yield break;

        foreach (var resource in response.Resources)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var resourcePath = WebDavHelpers.NormalizePath(WebDavHelpers.UriToPath(resource.Uri));

            if (resourcePath == Path)
                continue;

            if (resource.IsCollection)
            {
                if (type is StorableType.All or StorableType.Folder)
                    yield return new WebDavFolder(_webDavClient, resourcePath);

                continue;
            }

            if (type is StorableType.All or StorableType.File)
                yield return new WebDavFile(_webDavClient, resourcePath);
        }
    }

    public async Task<IFolder?> GetParentAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var parentPath = global::System.IO.Path.GetDirectoryName(Path)?.Replace('\\', '/');

        if (string.IsNullOrEmpty(parentPath))
            return null;

        var item = await _webDavClient.GetStorableFromPathAsync(parentPath, cancellationToken).ConfigureAwait(false);

        return item as IFolder;
    }
}
