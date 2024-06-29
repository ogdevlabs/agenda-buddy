namespace EventAndCommands;

public static class ConfigurationLoader
{
    public static LibrarySettings LoadConfiguration()
    {
        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls13;

        var basePath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

        var builder = new ConfigurationBuilder()
            .SetBasePath(basePath!)
            .AddJsonFile("appsettings.json", false, true);

        IConfiguration configuration = builder.Build();
        var mongoDbSettings = new MongoDbSettings
        {
            ConnectionString = configuration
                .GetSection("LibrarySettings")
                .GetSection("MongoDB")["ConnectionString"]!,
            DatabaseName = configuration
                .GetSection("LibrarySettings")
                .GetSection("MongoDB")["DatabaseName"]!,
            CollectionName = configuration
                .GetSection("LibrarySettings")
                .GetSection("MongoDB")["EventsCollection"]!
        };
        var librarySettings = new LibrarySettings
        {
            MongoDbSettings = mongoDbSettings
        };
        return librarySettings;
    }
}