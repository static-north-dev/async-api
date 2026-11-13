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
builder.Services.AddAsyncApi(_ =>
{
    var document = new AsyncApiDocumentBuilder(info => info.Info("Web Orders", "1.0.0"));
    AsyncApiDefinitions.Register(document);
    return document;
});

var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();
app
    .MapAsyncApi(builder.Configuration["AsyncApi:Path"] ?? "/asyncapi.json")
    .RequireAuthorization("InternalApi");
await app.RunAsync();
