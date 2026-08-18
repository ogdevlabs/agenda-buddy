using AgendaBuddy.AppHost;

var builder = DistributedApplication.CreateBuilder(args);

AppHostWiring.Configure(builder);

builder.Build().Run();
