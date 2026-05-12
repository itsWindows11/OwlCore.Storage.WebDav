using WebDav;

namespace OwlCore.Storage.WebDav;

internal sealed class WebDavCreatedAtProperty(IStorable owner, IWebDavClient client, string path)
    : SimpleStorageProperty<DateTime?>(
        id: owner.Id + "/" + nameof(ICreatedAt.CreatedAt),
        name: nameof(ICreatedAt.CreatedAt),
        asyncGetter: async ct =>
        {
            var resource = await client.GetResourceFromPathAsync(path, ct).ConfigureAwait(false);
            return WebDavHelpers.NormalizeDateTime(resource?.CreationDate);
        }),
    ICreatedAtProperty;

internal sealed class WebDavCreatedAtOffsetProperty(IStorable owner, IWebDavClient client, string path)
    : SimpleStorageProperty<DateTimeOffset?>(
        id: owner.Id + "/" + nameof(ICreatedAtOffset.CreatedAtOffset),
        name: nameof(ICreatedAtOffset.CreatedAtOffset),
        asyncGetter: async ct =>
        {
            var resource = await client.GetResourceFromPathAsync(path, ct).ConfigureAwait(false);
            return WebDavHelpers.NormalizeDateTimeOffset(resource?.CreationDate);
        }),
    ICreatedAtOffsetProperty;

internal sealed class WebDavLastModifiedAtProperty(IStorable owner, IWebDavClient client, string path)
    : SimpleStorageProperty<DateTime?>(
        id: owner.Id + "/" + nameof(ILastModifiedAt.LastModifiedAt),
        name: nameof(ILastModifiedAt.LastModifiedAt),
        asyncGetter: async ct =>
        {
            var resource = await client.GetResourceFromPathAsync(path, ct).ConfigureAwait(false);
            return WebDavHelpers.NormalizeDateTime(resource?.LastModifiedDate);
        }),
    ILastModifiedAtProperty;

internal sealed class WebDavLastModifiedAtOffsetProperty(IStorable owner, IWebDavClient client, string path)
    : SimpleStorageProperty<DateTimeOffset?>(
        id: owner.Id + "/" + nameof(ILastModifiedAtOffset.LastModifiedAtOffset),
        name: nameof(ILastModifiedAtOffset.LastModifiedAtOffset),
        asyncGetter: async ct =>
        {
            var resource = await client.GetResourceFromPathAsync(path, ct).ConfigureAwait(false);
            return WebDavHelpers.NormalizeDateTimeOffset(resource?.LastModifiedDate);
        }),
    ILastModifiedAtOffsetProperty;

internal sealed class WebDavLastAccessedAtProperty(IStorable owner, IWebDavClient client, string path)
    : SimpleStorageProperty<DateTime?>(
        id: owner.Id + "/" + nameof(ILastAccessedAt.LastAccessedAt),
        name: nameof(ILastAccessedAt.LastAccessedAt),
        asyncGetter: async ct =>
        {
            var resource = await client.GetResourceFromPathAsync(path, ct).ConfigureAwait(false);
            return WebDavHelpers.GetLastAccessedAtOffset(resource)?.LocalDateTime;
        }),
    ILastAccessedAtProperty;

internal sealed class WebDavLastAccessedAtOffsetProperty(IStorable owner, IWebDavClient client, string path)
    : SimpleStorageProperty<DateTimeOffset?>(
        id: owner.Id + "/" + nameof(ILastAccessedAtOffset.LastAccessedAtOffset),
        name: nameof(ILastAccessedAtOffset.LastAccessedAtOffset),
        asyncGetter: async ct =>
        {
            var resource = await client.GetResourceFromPathAsync(path, ct).ConfigureAwait(false);
            return WebDavHelpers.GetLastAccessedAtOffset(resource);
        }),
    ILastAccessedAtOffsetProperty;
