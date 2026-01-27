var builder = DistributedApplication.CreateBuilder(args);

var cache = builder.AddRedis("cache");

var apiService = builder.AddProject<Projects.XuThiWebApp_ApiService>("apiservice")
    .WithHttpHealthCheck("/health");

var frontend = builder.AddJavaScriptApp("xuthi-frontend", "../xuthi-frontend", "dev")
    .WithNpm().WaitFor(apiService)
    .WithReference(apiService)
    .WithHttpEndpoint(env: "PORT")
    .WithExternalHttpEndpoints();

builder.Build().Run();
