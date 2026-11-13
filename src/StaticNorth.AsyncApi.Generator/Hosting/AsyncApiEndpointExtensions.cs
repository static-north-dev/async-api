using System;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using StaticNorth.AsyncApi.Core;

namespace StaticNorth.AsyncApi.Generator;

internal static class AsyncApiEndpointExtensions
{
    public static IServiceCollection AddAsyncApi(
        this IServiceCollection services,
        Func<IServiceProvider, AsyncApiDocumentBuilder> createBuilder)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(createBuilder);
        return services.AddScoped(createBuilder);
    }

    public static RouteHandlerBuilder MapAsyncApi(this IEndpointRouteBuilder endpoints, string pattern = "/asyncapi.json")
    {
        return endpoints.MapGet(pattern, Handle);
    }

    public static RouteHandlerBuilder MapAsyncApiSnapshot(
        this IEndpointRouteBuilder endpoints,
        AsyncApiDocumentSnapshot snapshot,
        string pattern = "/asyncapi.json")
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return endpoints.MapGet(pattern, HandleSnapshot);

        IResult HandleSnapshot(HttpContext context)
        {
            context.Response.Headers.CacheControl = "no-store";
            var result = snapshot.GetResponse();
            return result;
        }
    }

    private static IResult Handle([FromServices] AsyncApiDocumentBuilder builder, HttpContext context)
    {
        context.Response.Headers.CacheControl = "no-store";

        try
        {
            var document = builder.Build();
            return Results.Bytes(AsyncApiJson.Write(document), "application/json");
        }
        catch (Exception exception) when (exception is InvalidOperationException or JsonException or ArgumentException or NotSupportedException)
        {
            return Results.Problem(statusCode: StatusCodes.Status500InternalServerError,
                title: "The AsyncAPI document could not be built.");
        }
    }
}
