namespace EventAndCommands;

public static class ConfigurationLoader
{
    public static LibrarySettings LoadConfiguration()
    {
        var basePath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        var builder = new ConfigurationBuilder()
            .SetBasePath(basePath!)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

        IConfiguration configuration = builder.Build();
        
        var mongoDbSettings = new MongoDbSettings
        {
            ConnectionString = configuration
                .GetSection("LibrarySettings")
                .GetSection("MongoDbSettings")["ConnectionString"]!,
            DatabaseName = configuration
                .GetSection("LibrarySettings")
                .GetSection("MongoDbSettings")["DatabaseName"]!,
            CollectionName = configuration
                .GetSection("LibrarySettings")
                .GetSection("MongoDbSettings")["CollectionName"]!
        };
        var librarySettings = new LibrarySettings
        {
            MongoDbSettings = mongoDbSettings
        };
        return librarySettings;
    }
}