var builder = DistributedApplication.CreateBuilder(args);

var cache = builder.AddRedis("cache");

// Add PostgreSQL for ProductCatalog
var postgres = builder.AddPostgres("postgres")
    .WithPgAdmin();

var productCatalogDb = postgres.AddDatabase("ProductCatalogDb");

var apiService = builder.AddProject<Projects.XuThiWebApp_ApiService>("apiservice")
    .WithReference(productCatalogDb)
    .WithHttpHealthCheck("/health");

var frontend = builder.AddJavaScriptApp("xuthi-frontend", "../xuthi-frontend", "dev")
    .WithNpm(installArgs: ["--legacy-peer-deps"])
    .WaitFor(apiService)
    .WithReference(apiService)
    .WithHttpEndpoint(env: "PORT")
    .WithExternalHttpEndpoints();

builder.Build().Run();