using OwlCore.Storage.CommonTests;

namespace OwlCore.Storage.WebDav.Tests;

[TestClass]
public sealed class WebDavCommonIFileTests : CommonIFileTests
{
    public override Task<IFile> CreateFileAsync()
    {
        var client = new InMemoryWebDavClient();
        client.AddFolder("/root");
        client.AddFile("/root/file.txt", "seed-content"u8.ToArray());
        return Task.FromResult<IFile>(new WebDavFile(client, "/root/file.txt"));
    }

    public override Task<IFile?> CreateFileWithCreatedAtAsync(DateTime createdAt)
    {
        var client = new InMemoryWebDavClient();
        client.AddFolder("/root");
        client.AddFile("/root/file.txt", "seed-content"u8.ToArray(), createdAt: createdAt);
        return Task.FromResult<IFile?>(new WebDavFile(client, "/root/file.txt"));
    }

    public override Task<IFile?> CreateFileWithLastModifiedAtAsync(DateTime lastModifiedAt)
    {
        var client = new InMemoryWebDavClient();
        client.AddFolder("/root");
        client.AddFile("/root/file.txt", "seed-content"u8.ToArray(), lastModifiedAt: lastModifiedAt);
        return Task.FromResult<IFile?>(new WebDavFile(client, "/root/file.txt"));
    }

    public override Task<IFile?> CreateFileWithLastAccessedAtAsync(DateTime lastAccessedAt)
    {
        var client = new InMemoryWebDavClient();
        client.AddFolder("/root");
        client.AddFile("/root/file.txt", "seed-content"u8.ToArray(), lastAccessedAt: lastAccessedAt);
        return Task.FromResult<IFile?>(new WebDavFile(client, "/root/file.txt"));
    }
}
