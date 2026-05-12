using Moq;
using System.Xml.Linq;
using WebDav;

namespace OwlCore.Storage.WebDav.Tests;

[TestClass]
public class WebDavDatePropertiesTests
{
    [TestMethod]
    public async Task FileDateProperties_ReturnExpectedValues()
    {
        var created = new DateTime(2026, 01, 02, 03, 04, 05, DateTimeKind.Utc);
        var modified = new DateTime(2026, 02, 03, 04, 05, 06, DateTimeKind.Utc);
        var client = new Mock<IWebDavClient>(MockBehavior.Strict);
        client.Setup(x => x.Propfind("/file.txt", It.IsAny<PropfindParameters>()))
            .ReturnsAsync(new PropfindResponse(207, new[]
            {
                new WebDavResource.Builder()
                    .WithUri("/file.txt")
                    .IsNotCollection()
                    .WithCreationDate(created)
                    .WithLastModifiedDate(modified)
                    .Build()
            }));

        var file = new WebDavFile(client.Object, "/file.txt");

        var createdAt = await file.CreatedAt.GetValueAsync();
        var modifiedAt = await file.LastModifiedAt.GetValueAsync();

        Assert.AreEqual(created.ToLocalTime(), createdAt);
        Assert.AreEqual(modified.ToLocalTime(), modifiedAt);
    }

    [TestMethod]
    public async Task FolderLastAccessedAtOffset_UsesWebDavPropertyValue()
    {
        var client = new Mock<IWebDavClient>(MockBehavior.Strict);
        client.Setup(x => x.Propfind("/folder", It.IsAny<PropfindParameters>()))
            .ReturnsAsync(new PropfindResponse(207, new[]
            {
                new WebDavResource.Builder()
                    .WithUri("/folder/")
                    .IsCollection()
                    .WithProperties(new[]
                    {
                        new WebDavProperty(XName.Get("getlastaccessed", "DAV:"), "Wed, 21 Oct 2015 07:28:00 GMT")
                    })
                    .Build()
            }));

        var folder = new WebDavFolder(client.Object, "/folder");

        var lastAccessed = await folder.LastAccessedAtOffset.GetValueAsync();

        Assert.IsNotNull(lastAccessed);
        Assert.AreEqual(new DateTimeOffset(2015, 10, 21, 7, 28, 0, TimeSpan.Zero).ToLocalTime(), lastAccessed.Value);
    }
}
