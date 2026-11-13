using System.Text;
using System.Text.Json;

namespace StaticNorth.AsyncApi.Core.Tests;

public sealed class AsyncApiJsonTests
{
    [Test]
    public async Task ReadsTheOrdersContract()
    {
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "orders.asyncapi.json");
        var fixture = await File.ReadAllBytesAsync(fixturePath);
        var document = AsyncApiJson.Read(fixture);
        var address = document.Channels?
            .GetProperty("orders")
            .GetProperty("address")
            .GetString();
        var action = document.Operations?
            .GetProperty("publishOrderCreated")
            .GetProperty("action")
            .GetString();
        var messages = document.Components?.GetProperty("messages");

        await Assert
            .That(document.SpecificationVersion)
            .IsEqualTo("3.1.0");
        await Assert
            .That(document.Info.Title)
            .IsEqualTo("Orders");
        await Assert
            .That(document.Info.Version)
            .IsEqualTo("1.0.0");
        await Assert
            .That(address)
            .IsEqualTo("orders.events");
        await Assert
            .That(action)
            .IsEqualTo("send");
        await Assert
            .That(messages?.TryGetProperty("OrderCreated", out _))
            .IsTrue();
    }

    [Test]
    public async Task WritesObjectsInOrdinalOrderAndPreservesArrayOrderAndNumbers()
    {
        var original = AsyncApiJson.Read("""
            {
              "x-data": { "z": ["second", "first", 1.00], "A": true },
              "info": { "version": "1", "title": "T" },
              "asyncapi": "3.1.0"
            }
            """u8.ToArray());
        var expected = """
            {"asyncapi":"3.1.0","info":{"title":"T","version":"1"},"x-data":{"A":true,"z":["second","first",1.00]}}
            """;
        var written = AsyncApiJson.Write(original);
        var rewritten = AsyncApiJson.Write(AsyncApiJson.Read(written));

        await Assert
            .That(Encoding.UTF8.GetString(written))
            .IsEqualTo(expected);
        await Assert
            .That(Encoding.UTF8.GetString(rewritten))
            .IsEqualTo(expected);
    }

    [Test]
    public async Task PreservesOpaqueBindingsAndLocalReferences()
    {
        var document = AsyncApiJson.Read("""
            {
              "asyncapi": "3.1.0",
              "info": { "title": "T", "version": "1", "x-info": true },
              "channels": {
                "orders": {
                  "bindings": { "example": { "setting": 1 } },
                  "messages": { "created": { "$ref": "#/components/messages/created" } }
                }
              },
              "components": { "messages": { "created": { "name": "created" } } }
            }
            """u8.ToArray());
        var output = AsyncApiJson.Write(document);

        using (var written = JsonDocument.Parse(output))
        {
            await Assert
                .That(JsonElement.DeepEquals(document.Root, written.RootElement))
                .IsTrue();
        }
    }

    [Test]
    [Arguments("{}")]
    [Arguments("[]")]
    [Arguments("{\"asyncapi\":\"3.0.0\",\"info\":{\"title\":\"T\",\"version\":\"1\"}}")]
    [Arguments("{\"asyncapi\":\"3.1.0\",\"info\":{\"title\":\"T\"}}")]
    [Arguments("{\"asyncapi\":\"3.1.0\",\"info\":{\"title\":1,\"version\":\"1\"}}")]
    [Arguments("{\"asyncapi\":\"3.1.0\",\"info\":{\"title\":\"T\",\"version\":\"1\"},\"channels\":[]}")]
    [Arguments("{\"asyncapi\":\"3.1.0\",\"info\":{\"title\":\"T\",\"version\":\"1\",\"title\":\"Duplicate\"}}")]
    [Arguments("{\"asyncapi\":")]
    public async Task RejectsInvalidDocumentRootsAndMalformedJson(string json)
    {
        var input = Encoding.UTF8.GetBytes(json);

        await Assert
            .That(() => AsyncApiJson.Read(input))
            .Throws<JsonException>();
    }
}
