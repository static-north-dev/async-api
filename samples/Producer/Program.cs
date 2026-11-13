using System.Text.Json.Nodes;
using StaticNorth.AsyncApi.Core;
using StaticNorth.AsyncApi.Generated;
using StaticNorth.AsyncApi.Generator;

if (args.Length != 1 || args[0] != "export")
{
    Console.Error.WriteLine("Usage: Producer export");
    return 1;
}

var builder = new AsyncApiDocumentBuilder(document =>
{
    document
        .Info("Orders", "1.0.0")
        .DefaultContentType("application/json")
        .Tag("orders", "Order events")
        .Server("broker", server =>
        {
            server
                .Host("broker.example.com:4222")
                .Protocol("nats");
        })
        .SecurityScheme("token", new JsonObject { ["type"] = "http", ["scheme"] = "bearer" })
        .OperationTrait("documented", new JsonObject { ["summary"] = "Documents the order event" })
        .WithExtension("x-staticnorth-example", new JsonObject { ["milestone"] = "M2" });
});
AsyncApiDefinitions.Register(builder);
var document = builder.Build();

using (var output = Console.OpenStandardOutput())
{
    await output.WriteAsync(AsyncApiJson.Write(document));
}

return 0;
