namespace EventsAndCommands.Tests;

public class ConfigurationLoaderTests
{
    [Fact]
    public void GetConfigurationFromAppSettingsSuccessfull()
    {
        var config = ConfigurationLoader.LoadConfiguration();
        Assert.IsType<LibrarySettings>(config);
    }
}
