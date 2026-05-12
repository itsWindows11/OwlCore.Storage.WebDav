using OwlCore.Storage.CommonTests;
using System.Text;

namespace OwlCore.Storage.WebDav.Tests;

[TestClass]
public sealed class WebDavCommonIFolderTests : CommonIFolderTests
{
    public override Task<IFolder> CreateFolderAsync()
    {
        var client = new InMemoryWebDavClient();
        client.AddFolder("/parent");
        client.AddFolder("/parent/folder");
        return Task.FromResult<IFolder>(new WebDavFolder(client, "/parent/folder"));
    }

    public override Task<IFolder> CreateFolderWithItems(int fileCount, int folderCount)
    {
        var client = new InMemoryWebDavClient();
        client.AddFolder("/parent");
        client.AddFolder("/parent/folder");

        for (var i = 0; i < fileCount; i++)
            client.AddFile($"/parent/folder/file{i}.txt", Encoding.UTF8.GetBytes($"seed-{i}"));

        for (var i = 0; i < folderCount; i++)
            client.AddFolder($"/parent/folder/child{i}");

        return Task.FromResult<IFolder>(new WebDavFolder(client, "/parent/folder"));
    }

    public override Task<IFolder?> CreateFolderWithCreatedAtAsync(DateTime createdAt)
    {
        var client = new InMemoryWebDavClient();
        client.AddFolder("/parent");
        client.AddFolder("/parent/folder", createdAt: createdAt);
        return Task.FromResult<IFolder?>(new WebDavFolder(client, "/parent/folder"));
    }

    public override Task<IFolder?> CreateFolderWithLastModifiedAtAsync(DateTime lastModifiedAt)
    {
        var client = new InMemoryWebDavClient();
        client.AddFolder("/parent");
        client.AddFolder("/parent/folder", lastModifiedAt: lastModifiedAt);
        return Task.FromResult<IFolder?>(new WebDavFolder(client, "/parent/folder"));
    }

    public override Task<IFolder?> CreateFolderWithLastAccessedAtAsync(DateTime lastAccessedAt)
    {
        var client = new InMemoryWebDavClient();
        client.AddFolder("/parent");
        client.AddFolder("/parent/folder", lastAccessedAt: lastAccessedAt);
        return Task.FromResult<IFolder?>(new WebDavFolder(client, "/parent/folder"));
    }
}
