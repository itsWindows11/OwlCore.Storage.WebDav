using Moq;
using OwlCore.Storage;
using WebDav;

namespace OwlCore.Storage.WebDav.Tests;

[TestClass]
public class WebDavFolderTests
{
    [TestMethod]
    public async Task GetItemsAsync_SkipsSelfResource()
    {
        var client = new Mock<IWebDavClient>(MockBehavior.Strict);
        client.Setup(x => x.Propfind("/root", It.IsAny<PropfindParameters>()))
            .ReturnsAsync(new PropfindResponse(207, new[]
            {
                CreateResource("/root/", isCollection: true),
                CreateResource("/root/child-folder/", isCollection: true),
                CreateResource("/root/child-file.txt", isCollection: false)
            }));

        var folder = new WebDavFolder(client.Object, "/root");

        var items = new List<IStorableChild>();
        await foreach (var item in folder.GetItemsAsync())
            items.Add(item);

        Assert.AreEqual(2, items.Count);
        Assert.IsTrue(items.Any(x => x is WebDavFolder && x.Id == "/root/child-folder"));
        Assert.IsTrue(items.Any(x => x is WebDavFile && x.Id == "/root/child-file.txt"));
        client.VerifyAll();
    }

    [TestMethod]
    public async Task GetItemAsync_ThrowsWhenIdOutsideFolder()
    {
        var client = new Mock<IWebDavClient>(MockBehavior.Strict);
        var folder = new WebDavFolder(client.Object, "/root");

        await Assert.ThrowsExactlyAsync<FileNotFoundException>(() => folder.GetItemAsync("/other/file.txt"));
    }

    private static WebDavResource CreateResource(string uri, bool isCollection)
    {
        var builder = new WebDavResource.Builder().WithUri(uri);

        if (isCollection)
            builder.IsCollection();
        else
            builder.IsNotCollection();

        return builder.Build();
    }
}
