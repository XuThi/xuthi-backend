using Carter;
using Core.Extensions;
using ProductCatalog;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddProblemDetails();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// Add MediatR with all module assemblies
builder.Services.AddMediatRWithAssemblies(
    typeof(ProductCatalogModule).Assembly
);

// Add Carter for minimal API endpoints
builder.Services.AddCarterWithAssemblies(
    typeof(ProductCatalogModule).Assembly
);

// Add ProductCatalog module (DbContext via Aspire)
builder.AddProductCatalogModule();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Map Carter endpoints from all modules
app.MapCarter();

app.MapDefaultEndpoints();

app.Run();
