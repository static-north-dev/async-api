using Microsoft.AspNetCore.Authentication;
using StaticNorth.AsyncApi.Generated;
using StaticNorth.AsyncApi.Generator;

var builder = WebApplication.CreateBuilder(args);
builder.Services
    .AddAuthentication("SampleToken")
    .AddScheme<AuthenticationSchemeOptions, SampleTokenHandler>("SampleToken", _ => { });
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("InternalApi", policy => policy.RequireAuthenticatedUser());
});
var useSnapshot = builder.Configuration.GetValue<bool>("AsyncApi:UseSnapshot");
if (!useSnapshot)
{
    builder.Services.AddAsyncApi(_ => CreateDocumentBuilder());
}

var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();
var path = builder.Configuration["AsyncApi:Path"] ?? "/asyncapi.json";
var endpoint = useSnapshot
    ? app.MapAsyncApiSnapshot(new AsyncApiDocumentSnapshot(CreateDocumentBuilder().Build()), path)
    : app.MapAsyncApi(path);
endpoint
    .WithName(AsyncApiViewerEndpoints.DocumentEndpointName)
    .RequireAuthorization("InternalApi");
app.MapAsyncApiViewer();
await app.RunAsync();

static AsyncApiDocumentBuilder CreateDocumentBuilder()
{
    var document = new AsyncApiDocumentBuilder(info => info.Info("Web Orders", "1.0.0"));
    AsyncApiDefinitions.Register(document);
    return document;
}
