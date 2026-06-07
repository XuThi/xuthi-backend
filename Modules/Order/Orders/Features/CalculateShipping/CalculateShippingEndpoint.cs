using Carter;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Order.Orders.Models;
using System;

namespace Order.Orders.Features.CalculateShipping;

public class CalculateShippingEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/orders/calculate-shipping", async (
            CalculateShippingRequest request,
            ISender sender) =>
        {
            var query = new CalculateShippingQuery(
                request.PaymentMethod,
                request.ShippingCity,
                request.ShippingWard,
                request.Items.Select(i => new CalculateShippingItem(i.ProductId, i.VariantId, i.Quantity)).ToList(),
                request.ShippingDistrict
            );
            var result = await sender.Send(query);
            return Results.Ok(result);
        })
        .WithName("CalculateShippingFee")
        .WithTags("Orders")
        .WithSummary("Calculate shipping fee based on payment method, location, items, and GHN settings");

        app.MapGet("/api/orders/test-ghn", async (
            ISender sender,
            Microsoft.Extensions.Configuration.IConfiguration config) =>
        {
            var token = config["GHN:Token"] ?? config["GHN_TOKEN"] ?? Environment.GetEnvironmentVariable("GHN_TOKEN");
            var hasToken = !string.IsNullOrWhiteSpace(token);
            var tokenSnippet = hasToken ? token.Substring(0, Math.Min(token.Length, 5)) + "..." : "none";
            
            var query = new CalculateShippingQuery(
                PaymentMethod.CashOnDelivery,
                "Thành phố Hồ Chí Minh",
                "Phường Tân Thới Nhất",
                new List<CalculateShippingItem>(),
                "Quận 12"
            );
            
            try 
            {
                var result = await sender.Send(query);
                return Results.Ok(new {
                    success = true,
                    hasToken,
                    tokenLength = hasToken ? token.Length : 0,
                    tokenSnippet,
                    shippingFee = result.ShippingFee,
                    isGhnUsed = result.IsGhnUsed
                });
            }
            catch (Exception ex)
            {
                return Results.Ok(new {
                    success = false,
                    hasToken,
                    tokenSnippet,
                    error = ex.Message,
                    stackTrace = ex.StackTrace
                });
            }
        })
        .WithName("TestGhnResolution")
        .WithTags("Orders");
    }
}

public record CalculateShippingRequest(
    PaymentMethod PaymentMethod,
    string ShippingCity,
    string ShippingWard,
    List<CalculateShippingItemDto> Items,
    string? ShippingDistrict = null
);

public record CalculateShippingItemDto(
    Guid ProductId,
    Guid VariantId,
    int Quantity
);
