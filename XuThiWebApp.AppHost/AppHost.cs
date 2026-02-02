var builder = DistributedApplication.CreateBuilder(args);

var cache = builder.AddRedis("cache");

var postgres = builder.AddPostgres("db")
    .AddDatabase("appdata");

// Add Keycloak for authentication with realm import
// var keycloak = builder.AddKeycloak("keycloak", 8080)
//     .WithDataVolume()
//     .WithRealmImport("./keycloak");

var apiService = builder.AddProject<Projects.XuThiWebApp_ApiService>("apiservice")
    .WithReference(postgres)
    // .WithReference(keycloak)
    .WaitFor(postgres)
    // .WaitFor(keycloak)      // Wait for Keycloak too
    .WithHttpHealthCheck("/health");

var frontend = builder.AddJavaScriptApp("xuthi-frontend", "../xuthi-frontend", "dev")
    .WithNpm(installArgs: ["--legacy-peer-deps"])
    .WaitFor(apiService)
    .WithReference(apiService)
    .WithHttpEndpoint(env: "PORT")
    .WithExternalHttpEndpoints();

builder.Build().Run();