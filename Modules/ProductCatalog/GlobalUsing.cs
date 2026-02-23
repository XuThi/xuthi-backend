// DDD aggregate-rooted models
global using ProductCatalog.Products.Models;
global using ProductCatalog.Products.Dtos;
global using ProductCatalog.Products.Events;
global using ProductCatalog.Brands.Models;
global using ProductCatalog.Brands.Dtos;
global using ProductCatalog.Categories.Models;
global using ProductCatalog.Categories.Dtos;
global using ProductCatalog.Groups.Models;
global using ProductCatalog.Groups.Dtos;
global using ProductCatalog.VariantOptions.Models;
global using ProductCatalog.VariantOptions.Dtos;
global using ProductCatalog.Data;

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