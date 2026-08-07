namespace PixelPress.Core.Tests;

public class BootstrapTests
{
    [Fact]
    public void Placeholder_Message_IsAvailable()
    {
        Assert.Equal("PixelPress.Core bootstrapped", Placeholder.Message);
    }
}
