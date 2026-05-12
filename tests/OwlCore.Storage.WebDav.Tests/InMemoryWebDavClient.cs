using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Xml.Linq;
using WebDav;

namespace OwlCore.Storage.WebDav.Tests;

internal sealed class InMemoryWebDavClient : IWebDavClient
{
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);

    public InMemoryWebDavClient()
    {
        var now = DateTime.UtcNow;
        _entries["/"] = new Entry(true, Array.Empty<byte>(), now, now, now);
    }

    public void AddFolder(string path, DateTime? createdAt = null, DateTime? lastModifiedAt = null, DateTime? lastAccessedAt = null)
    {
        path = Normalize(path);
        var now = DateTime.UtcNow;
        _entries[path] = new Entry(
            true,
            Array.Empty<byte>(),
            ToUtc(createdAt ?? now),
            ToUtc(lastModifiedAt ?? now),
            ToUtc(lastAccessedAt ?? now));
    }

    public void AddFile(string path, byte[] content, DateTime? createdAt = null, DateTime? lastModifiedAt = null, DateTime? lastAccessedAt = null)
    {
        path = Normalize(path);
        var now = DateTime.UtcNow;
        _entries[path] = new Entry(
            false,
            content,
            ToUtc(createdAt ?? now),
            ToUtc(lastModifiedAt ?? now),
            ToUtc(lastAccessedAt ?? now));
    }

    public Task<PropfindResponse> Propfind(string requestUri) => Propfind(requestUri, new PropfindParameters());

    public Task<PropfindResponse> Propfind(Uri requestUri) => Propfind(ToPath(requestUri), new PropfindParameters());

    public Task<PropfindResponse> Propfind(Uri requestUri, PropfindParameters parameters) => Propfind(ToPath(requestUri), parameters);

    public Task<PropfindResponse> Propfind(string requestUri, PropfindParameters parameters)
    {
        parameters.CancellationToken.ThrowIfCancellationRequested();

        var path = Normalize(requestUri);
        if (!_entries.TryGetValue(path, out _))
            return Task.FromResult(new PropfindResponse(404));

        var resources = new List<WebDavResource>();

        var includeChildren = parameters.ApplyTo == ApplyTo.Propfind.ResourceAndChildren;

        resources.Add(BuildResource(path));

        if (includeChildren)
        {
            foreach (var child in GetDirectChildren(path))
                resources.Add(BuildResource(child));
        }

        return Task.FromResult(new PropfindResponse(207, resources));
    }

    public Task<ProppatchResponse> Proppatch(string requestUri, ProppatchParameters parameters)
    {
        parameters.CancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new ProppatchResponse(501));
    }

    public Task<ProppatchResponse> Proppatch(Uri requestUri, ProppatchParameters parameters) => Proppatch(ToPath(requestUri), parameters);

    public Task<WebDavResponse> Mkcol(string requestUri) => Mkcol(requestUri, new MkColParameters());

    public Task<WebDavResponse> Mkcol(Uri requestUri) => Mkcol(ToPath(requestUri), new MkColParameters());

    public Task<WebDavResponse> Mkcol(Uri requestUri, MkColParameters parameters) => Mkcol(ToPath(requestUri), parameters);

    public Task<WebDavResponse> Mkcol(string requestUri, MkColParameters parameters)
    {
        parameters.CancellationToken.ThrowIfCancellationRequested();

        var path = Normalize(requestUri);
        if (_entries.TryGetValue(path, out var existing))
            return Task.FromResult(new WebDavResponse(existing.IsCollection ? 200 : 409));

        var parent = Parent(path);
        if (!_entries.TryGetValue(parent, out var parentEntry) || !parentEntry.IsCollection)
            return Task.FromResult(new WebDavResponse(409));

        var now = DateTime.UtcNow;
        _entries[path] = new Entry(true, Array.Empty<byte>(), now, now, now);
        return Task.FromResult(new WebDavResponse(201));
    }

    public Task<WebDavStreamResponse> GetRawFile(string requestUri) => GetRawFile(requestUri, new GetFileParameters());

    public Task<WebDavStreamResponse> GetRawFile(Uri requestUri) => GetRawFile(ToPath(requestUri), new GetFileParameters());

    public Task<WebDavStreamResponse> GetRawFile(Uri requestUri, GetFileParameters parameters) => GetRawFile(ToPath(requestUri), parameters);

    public Task<WebDavStreamResponse> GetRawFile(string requestUri, GetFileParameters parameters)
    {
        parameters.CancellationToken.ThrowIfCancellationRequested();

        var path = Normalize(requestUri);
        if (!_entries.TryGetValue(path, out var entry) || entry.IsCollection)
            return Task.FromResult(new WebDavStreamResponse(new HttpResponseMessage(HttpStatusCode.NotFound), Stream.Null));

        _entries[path] = entry with { LastAccessedAtUtc = DateTime.UtcNow };
        return Task.FromResult(new WebDavStreamResponse(new HttpResponseMessage(HttpStatusCode.OK), new MemoryStream(entry.Content, writable: false)));
    }

    public Task<WebDavStreamResponse> GetProcessedFile(string requestUri) => GetRawFile(requestUri, new GetFileParameters());

    public Task<WebDavStreamResponse> GetProcessedFile(Uri requestUri) => GetRawFile(ToPath(requestUri), new GetFileParameters());

    public Task<WebDavStreamResponse> GetProcessedFile(string requestUri, GetFileParameters parameters) => GetRawFile(requestUri, parameters);

    public Task<WebDavStreamResponse> GetProcessedFile(Uri requestUri, GetFileParameters parameters) => GetRawFile(ToPath(requestUri), parameters);

    public Task<HttpResponseMessage> GetFileResponse(Uri requestUri, bool translate, GetFileParameters parameters)
    {
        parameters.CancellationToken.ThrowIfCancellationRequested();

        var path = Normalize(ToPath(requestUri));
        if (!_entries.TryGetValue(path, out var entry) || entry.IsCollection)
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));

        _entries[path] = entry with { LastAccessedAtUtc = DateTime.UtcNow };
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(entry.Content)
        };

        return Task.FromResult(response);
    }

    public Task<WebDavResponse> Delete(string requestUri) => Delete(requestUri, new DeleteParameters());

    public Task<WebDavResponse> Delete(Uri requestUri) => Delete(ToPath(requestUri), new DeleteParameters());

    public Task<WebDavResponse> Delete(Uri requestUri, DeleteParameters parameters) => Delete(ToPath(requestUri), parameters);

    public Task<WebDavResponse> Delete(string requestUri, DeleteParameters parameters)
    {
        parameters.CancellationToken.ThrowIfCancellationRequested();

        var path = Normalize(requestUri);
        if (path == "/")
            return Task.FromResult(new WebDavResponse(403));

        if (!_entries.ContainsKey(path))
            return Task.FromResult(new WebDavResponse(404));

        var prefix = path + "/";
        foreach (var key in _entries.Keys.Where(x => x == path || x.StartsWith(prefix, StringComparison.Ordinal)).ToList())
            _entries.Remove(key);

        return Task.FromResult(new WebDavResponse(204));
    }

    public Task<WebDavResponse> PutFile(string requestUri, Stream stream) => PutFile(requestUri, stream, new PutFileParameters());

    public Task<WebDavResponse> PutFile(Uri requestUri, Stream stream) => PutFile(ToPath(requestUri), stream, new PutFileParameters());

    public Task<WebDavResponse> PutFile(string requestUri, Stream stream, string contentType) => PutFile(requestUri, stream, new PutFileParameters());

    public Task<WebDavResponse> PutFile(Uri requestUri, Stream stream, string contentType) => PutFile(ToPath(requestUri), stream, new PutFileParameters());

    public Task<WebDavResponse> PutFile(Uri requestUri, Stream stream, PutFileParameters parameters) => PutFile(ToPath(requestUri), stream, parameters);

    public async Task<WebDavResponse> PutFile(string requestUri, Stream stream, PutFileParameters parameters)
    {
        parameters.CancellationToken.ThrowIfCancellationRequested();

        var path = Normalize(requestUri);
        var parent = Parent(path);
        if (!_entries.TryGetValue(parent, out var parentEntry) || !parentEntry.IsCollection)
            return new WebDavResponse(409);

        if (_entries.TryGetValue(path, out var existing) && existing.IsCollection)
            return new WebDavResponse(409);

        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, parameters.CancellationToken).ConfigureAwait(false);

        var now = DateTime.UtcNow;
        var created = existing?.CreatedAtUtc ?? now;
        _entries[path] = new Entry(false, ms.ToArray(), created, now, now);
        return new WebDavResponse(existing is null ? 201 : 204);
    }

    public Task<WebDavResponse> PutFile(string requestUri, HttpContent content) => PutFile(requestUri, content, new PutFileParameters());

    public Task<WebDavResponse> PutFile(Uri requestUri, HttpContent content) => PutFile(ToPath(requestUri), content, new PutFileParameters());

    public Task<WebDavResponse> PutFile(Uri requestUri, HttpContent content, PutFileParameters parameters) => PutFile(ToPath(requestUri), content, parameters);

    public async Task<WebDavResponse> PutFile(string requestUri, HttpContent content, PutFileParameters parameters)
    {
        await using var stream = await content.ReadAsStreamAsync(parameters.CancellationToken).ConfigureAwait(false);
        return await PutFile(requestUri, stream, parameters).ConfigureAwait(false);
    }

    public Task<WebDavResponse> Copy(string sourceUri, string destUri) => Copy(sourceUri, destUri, new CopyParameters());

    public Task<WebDavResponse> Copy(Uri sourceUri, Uri destUri) => Copy(ToPath(sourceUri), ToPath(destUri), new CopyParameters());

    public Task<WebDavResponse> Copy(Uri sourceUri, Uri destUri, CopyParameters parameters) => Copy(ToPath(sourceUri), ToPath(destUri), parameters);

    public Task<WebDavResponse> Copy(string sourceUri, string destUri, CopyParameters parameters)
    {
        parameters.CancellationToken.ThrowIfCancellationRequested();

        var source = Normalize(sourceUri);
        var destination = Normalize(destUri);
        if (!_entries.TryGetValue(source, out var sourceEntry))
            return Task.FromResult(new WebDavResponse(404));

        if (_entries.ContainsKey(destination) && !parameters.Overwrite)
            return Task.FromResult(new WebDavResponse(412));

        RemoveSubtree(destination);

        if (sourceEntry.IsCollection)
        {
            foreach (var entry in GetSubtree(source))
            {
                var suffix = entry.Key.Substring(source.Length);
                _entries[destination + suffix] = entry.Value;
            }
        }
        else
        {
            _entries[destination] = sourceEntry;
        }

        return Task.FromResult(new WebDavResponse(201));
    }

    public Task<WebDavResponse> Move(string sourceUri, string destUri) => Move(sourceUri, destUri, new MoveParameters());

    public Task<WebDavResponse> Move(Uri sourceUri, Uri destUri) => Move(ToPath(sourceUri), ToPath(destUri), new MoveParameters());

    public Task<WebDavResponse> Move(Uri sourceUri, Uri destUri, MoveParameters parameters) => Move(ToPath(sourceUri), ToPath(destUri), parameters);

    public async Task<WebDavResponse> Move(string sourceUri, string destUri, MoveParameters parameters)
    {
        var copyResponse = await Copy(sourceUri, destUri, new CopyParameters
        {
            Overwrite = parameters.Overwrite,
            CancellationToken = parameters.CancellationToken
        }).ConfigureAwait(false);

        if (!copyResponse.IsSuccessful)
            return copyResponse;

        return await Delete(sourceUri, new DeleteParameters { CancellationToken = parameters.CancellationToken }).ConfigureAwait(false);
    }

    public Task<LockResponse> Lock(string requestUri) => Lock(requestUri, new LockParameters());

    public Task<LockResponse> Lock(Uri requestUri) => Lock(ToPath(requestUri), new LockParameters());

    public Task<LockResponse> Lock(Uri requestUri, LockParameters parameters) => Lock(ToPath(requestUri), parameters);

    public Task<LockResponse> Lock(string requestUri, LockParameters parameters)
    {
        parameters.CancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new LockResponse(501));
    }

    public Task<WebDavResponse> Unlock(string requestUri, string lockToken) => Unlock(requestUri, new UnlockParameters(lockToken));

    public Task<WebDavResponse> Unlock(Uri requestUri, string lockToken) => Unlock(ToPath(requestUri), new UnlockParameters(lockToken));

    public Task<WebDavResponse> Unlock(Uri requestUri, UnlockParameters parameters) => Unlock(ToPath(requestUri), parameters);

    public Task<WebDavResponse> Unlock(string requestUri, UnlockParameters parameters)
    {
        parameters.CancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new WebDavResponse(501));
    }

    public Task<PropfindResponse> Search(string requestUri, SearchParameters parameters)
    {
        parameters.CancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new PropfindResponse(501));
    }

    public Task<PropfindResponse> Search(Uri requestUri, SearchParameters parameters) => Search(ToPath(requestUri), parameters);

    public void Dispose()
    {
    }

    private WebDavResource BuildResource(string path)
    {
        var entry = _entries[path];
        var uri = entry.IsCollection && path != "/" ? path + "/" : path;

        var builder = new WebDavResource.Builder()
            .WithUri(uri)
            .WithCreationDate(entry.CreatedAtUtc)
            .WithLastModifiedDate(entry.LastModifiedAtUtc)
            .WithProperties(new[]
            {
                new WebDavProperty(
                    XName.Get("getlastaccessed", "DAV:"),
                    entry.LastAccessedAtUtc.ToString("R", CultureInfo.InvariantCulture))
            });

        if (entry.IsCollection)
            builder.IsCollection();
        else
            builder.IsNotCollection();

        return builder.Build();
    }

    private Dictionary<string, Entry> GetSubtree(string root)
    {
        var prefix = root + "/";
        return _entries
            .Where(x => x.Key == root || x.Key.StartsWith(prefix, StringComparison.Ordinal))
            .ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);
    }

    private IEnumerable<string> GetDirectChildren(string path)
    {
        var prefix = path == "/" ? "/" : path + "/";
        return _entries.Keys
            .Where(x => x.StartsWith(prefix, StringComparison.Ordinal) && x != path)
            .Where(x => !x[prefix.Length..].Contains('/'));
    }

    private void RemoveSubtree(string root)
    {
        if (root == "/")
            return;

        var prefix = root + "/";
        foreach (var key in _entries.Keys.Where(x => x == root || x.StartsWith(prefix, StringComparison.Ordinal)).ToList())
            _entries.Remove(key);
    }

    private static string ToPath(Uri uri) => Normalize(uri.IsAbsoluteUri ? uri.AbsolutePath : uri.OriginalString);

    private static string Parent(string path)
    {
        if (path == "/")
            return "/";

        var index = path.LastIndexOf('/');
        if (index <= 0)
            return "/";

        return path[..index];
    }

    private static string Normalize(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return "/";

        var normalized = path.Replace('\\', '/');
        if (!normalized.StartsWith("/", StringComparison.Ordinal))
            normalized = "/" + normalized;

        while (normalized.Contains("//", StringComparison.Ordinal))
            normalized = normalized.Replace("//", "/", StringComparison.Ordinal);

        if (normalized.Length > 1 && normalized.EndsWith("/", StringComparison.Ordinal))
            normalized = normalized[..^1];

        return normalized;
    }

    private static DateTime ToUtc(DateTime dateTime) => dateTime.Kind switch
    {
        DateTimeKind.Utc => dateTime,
        DateTimeKind.Local => dateTime.ToUniversalTime(),
        _ => DateTime.SpecifyKind(dateTime, DateTimeKind.Utc)
    };

    private sealed record Entry(
        bool IsCollection,
        byte[] Content,
        DateTime CreatedAtUtc,
        DateTime LastModifiedAtUtc,
        DateTime LastAccessedAtUtc);
}
