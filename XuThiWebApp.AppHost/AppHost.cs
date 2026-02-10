var builder = DistributedApplication.CreateBuilder(args);

var cache = builder.AddRedis("cache");

var postgres = builder.AddPostgres("db")
    .AddDatabase("appdata");

var apiService = builder.AddProject<Projects.XuThiWebApp_ApiService>("apiservice")
    .WithReference(postgres)
    .WaitFor(postgres)
    .WithHttpHealthCheck("/health");

var frontend = builder.AddJavaScriptApp("xuthi-frontend", "../xuthi-frontend", "dev")
    .WithNpm(installArgs: ["--legacy-peer-deps"])
    .WaitFor(apiService)
    .WithReference(apiService)
    .WithHttpEndpoint(env: "PORT")
    .WithExternalHttpEndpoints();

// Inject the API URL for the frontend to use for auth callbacks
frontend.WithEnvironment("NEXT_PUBLIC_API_URL", apiService.GetEndpoint("https"));

builder.Build().Run();