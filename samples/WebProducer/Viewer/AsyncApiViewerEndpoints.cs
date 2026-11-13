using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Mvc;

internal static class AsyncApiViewerEndpoints
{
    public const string DocumentEndpointName = "AsyncApiDocument";
    private const string ViewerPath = "/asyncapi-ui";
    private const string IndexResource = "Viewer.index.html";

    public static void MapAsyncApiViewer(this IEndpointRouteBuilder endpoints)
    {
        using var index = typeof(AsyncApiViewerEndpoints).Assembly.GetManifestResourceStream(IndexResource);
        if (index is null)
        {
            return;
        }

        var viewer = endpoints.MapGroup(ViewerPath);
        viewer.MapGet("", GetPage);
        viewer.MapGet("/assets/{asset}", GetAsset);
    }

    private static async Task<IResult> GetPage(HttpContext context, [FromServices] LinkGenerator links)
    {
        var documentPath = links.GetPathByName(context, DocumentEndpointName);
        if (documentPath is null)
        {
            return Results.Problem(title: "The AsyncAPI document route could not be generated.");
        }

        using var stream = typeof(AsyncApiViewerEndpoints).Assembly.GetManifestResourceStream(IndexResource)!;
        using var reader = new StreamReader(stream);
        var template = await reader.ReadToEndAsync(context.RequestAborted);
        var assetsPath = context.Request.PathBase.Add(new PathString(ViewerPath + "/assets/"));
        var page = template
            .Replace("__ASSETS_PATH__", HtmlEncoder.Default.Encode(assetsPath.ToUriComponent()), StringComparison.Ordinal)
            .Replace("__DOCUMENT_PATH__", HtmlEncoder.Default.Encode(documentPath), StringComparison.Ordinal);

        // The bundled AsyncAPI parser uses Ajv to compile its validators in the browser.
        context.Response.Headers.ContentSecurityPolicy =
            "default-src 'none'; script-src 'self' 'unsafe-eval'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; " +
            "connect-src 'self'; base-uri 'none'; frame-ancestors 'none'; form-action 'none'";
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers["Referrer-Policy"] = "no-referrer";
        return Results.Content(page, "text/html; charset=utf-8");
    }

    private static IResult GetAsset([FromRoute] string asset)
    {
        var contentType = asset switch
        {
            "viewer.js" or "asyncapi.js" => "text/javascript; charset=utf-8",
            "viewer.css" or "asyncapi.css" => "text/css; charset=utf-8",
            "LICENSE.txt" or "index.js.LICENSE.txt" => "text/plain; charset=utf-8",
            _ => null,
        };

        if (contentType is null)
        {
            return Results.NotFound();
        }

        var stream = typeof(AsyncApiViewerEndpoints).Assembly.GetManifestResourceStream("Viewer." + asset)!;
        return Results.Stream(stream, contentType);
    }
}
