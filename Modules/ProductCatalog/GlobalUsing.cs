global using ProductCatalog.Infrastructure.Entity;
global using ProductCatalog.Infrastructure.Dtos;
global using ProductCatalog.Infrastructure.Data;
global using ProductCatalog.Events;

global using Microsoft.EntityFrameworkCore;
global using Microsoft.EntityFrameworkCore.Metadata.Builders;
global using Microsoft.AspNetCore.Builder;
global using Microsoft.AspNetCore.Http;
global using Microsoft.AspNetCore.Routing;
global using Microsoft.Extensions.Logging;
global using Microsoft.Extensions.Configuration;

global using Contracts.CQRS;
global using MediatR;
global using Mapster;
global using Carter;
global using FluentValidation;

global using System.Reflection;