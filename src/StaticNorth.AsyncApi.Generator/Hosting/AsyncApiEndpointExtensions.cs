using System;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using StaticNorth.AsyncApi.Core;

namespace StaticNorth.AsyncApi.Generator;

/// <summary>Registers and maps AsyncAPI document endpoints in an ASP.NET Core host.</summary>
internal static class AsyncApiEndpointExtensions
{
    /// <summary>Registers a scoped builder for the live AsyncAPI document endpoint.</summary>
    /// <param name="services">The host service collection.</param>
    /// <param name="createBuilder">A factory that creates a builder for each service scope.</param>
    /// <returns>The service collection for further registration.</returns>
    public static IServiceCollection AddAsyncApi(
        this IServiceCollection services,
        Func<IServiceProvider, AsyncApiDocumentBuilder> createBuilder)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(createBuilder);
        return services.AddScoped(createBuilder);
    }

    /// <summary>Maps a GET endpoint that builds and serves the AsyncAPI document per request.</summary>
    /// <param name="endpoints">The host's endpoint route builder.</param>
    /// <param name="pattern">The route pattern for the JSON document.</param>
    /// <returns>The route builder for applying host conventions such as authorization.</returns>
    public static RouteHandlerBuilder MapAsyncApi(this IEndpointRouteBuilder endpoints, string pattern = "/asyncapi.json")
    {
        return endpoints.MapGet(pattern, Handle);
    }

    /// <summary>Maps a GET endpoint that serves a shared, serialized AsyncAPI document.</summary>
    /// <param name="endpoints">The host's endpoint route builder.</param>
    /// <param name="snapshot">The document snapshot to serve and update.</param>
    /// <param name="pattern">The route pattern for the JSON document.</param>
    /// <returns>The route builder for applying host conventions such as authorization.</returns>
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
