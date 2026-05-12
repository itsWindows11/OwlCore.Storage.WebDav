using Moq;
using WebDav;

namespace OwlCore.Storage.WebDav.Tests;

[TestClass]
public class WebDavFileStaticTests
{
    [TestMethod]
    public async Task TryGetFromWebDavPathAsync_ReturnsNullWhenFolder()
    {
        var client = new Mock<IWebDavClient>(MockBehavior.Strict);
        client.Setup(x => x.Propfind("/folder", It.IsAny<PropfindParameters>()))
            .ReturnsAsync(new PropfindResponse(207, new[] { new WebDavResource.Builder().WithUri("/folder/").IsCollection().Build() }));

        var item = await WebDavFile.TryGetFromWebDavPathAsync(client.Object, "/folder");

        Assert.IsNull(item);
        client.VerifyAll();
    }

    [TestMethod]
    public void Constructor_NormalizesSeparatorsAndTrailingSlash()
    {
        var client = new Mock<IWebDavClient>(MockBehavior.Strict);

        var file = new WebDavFile(client.Object, "\\root\\child//");

        Assert.AreEqual("/root/child", file.Path);
    }
}
