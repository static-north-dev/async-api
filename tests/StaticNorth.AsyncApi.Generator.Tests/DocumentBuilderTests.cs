using System.Text.Json.Nodes;
using StaticNorth.AsyncApi.Core;

namespace StaticNorth.AsyncApi.Generator.Tests;

public sealed class DocumentBuilderTests
{
    [Test]
    public async Task BuildsFreshSnapshotsAndValidatesChannelMembership()
    {
        var builds = 0;
        var builder = new AsyncApiDocumentBuilder(document =>
        {
            builds++;
            document.Info("Tests", "1.0.0");
        });
        builder
            .AddChannel(() => new TestChannelDefinition())
            .AddOperation(() => new TestOperationDefinition());

        await Assert
            .That(builds)
            .IsEqualTo(0);
        await Assert
            .That(() => builder.Build())
            .Throws<InvalidOperationException>();
        await Assert
            .That(builds)
            .IsEqualTo(1);
    }

    [Test]
    public async Task EscapesChannelAndMessageKeysAndKeepsMessageIdsSeparateFromNames()
    {
        var schema = new JsonObject { ["type"] = "string" };
        var builder = new AsyncApiDocumentBuilder(document => document.Info("Tests", "1.0.0"));
        builder
            .AddMessage<string>(() => new TextMessageDefinition(schema, "first"))
            .AddMessage<string>(() => new TextMessageDefinition(schema, "second"))
            .AddChannel(() => new TestChannelDefinition("channel/~%#", "first", "message/~%#"))
            .AddOperation(() => new TestOperationDefinition("channel/~%#", "message/~%#"));
        var document = builder.Build();
        var channels = document.Channels!.Value;
        var channel = channels.GetProperty("channel/~%#");
        var messages = channel.GetProperty("messages");
        var operation = document.Operations!.Value.GetProperty("sendText");
        var channelReference = operation.GetProperty("channel");
        var operationMessages = operation.GetProperty("messages");
        var channelMessage = messages.GetProperty("message/~%#");

        await Assert
            .That(channelReference.GetProperty("$ref").GetString())
            .IsEqualTo("#/channels/channel~1~0%25%23");
        await Assert
            .That(operationMessages[0].GetProperty("$ref").GetString())
            .IsEqualTo("#/channels/channel~1~0%25%23/messages/message~1~0%25%23");
        await Assert
            .That(channelMessage.GetProperty("$ref").GetString())
            .IsEqualTo("#/components/messages/first");
    }

    [Test]
    public async Task RejectsDuplicateMessageIdsAndCanBuildAgainAfterFailure()
    {
        var id = "first";
        var schema = new JsonObject { ["type"] = "string" };
        var builder = new AsyncApiDocumentBuilder(document => document.Info("Tests", "1.0.0"));
        builder
            .AddMessage<string>(() => new TextMessageDefinition(schema, "first"))
            .AddMessage<string>(() => new TextMessageDefinition(schema, id));

        await Assert
            .That(() => builder.Build())
            .Throws<InvalidOperationException>();

        id = "second";
        var document = builder.Build();
        var messages = document.Components!.Value.GetProperty("messages");

        await Assert
            .That(messages.TryGetProperty("second", out _))
            .IsTrue();
    }

    [Test]
    public async Task RequiresAnExplicitSchemaForAnOpaquePayload()
    {
        var builder = new AsyncApiDocumentBuilder(document => document.Info("Tests", "1.0.0"));
        builder.AddMessage<object>(() => new OpaqueMessageDefinition());

        await Assert
            .That(() => builder.Build())
            .Throws<NotSupportedException>();
    }

    [Test]
    [Arguments("test.{region}")]
    [Arguments("test.{}")]
    [Arguments("test.{broken")]
    public async Task RejectsInvalidChannelAddressParameters(string address)
    {
        var builder = new AsyncApiDocumentBuilder(document => document.Info("Tests", "1.0.0"));
        builder.AddChannel(() => new TestChannelDefinition(address: address));

        await Assert
            .That(() => builder.Build())
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task SnapshotsExplicitSchemasAndKeepsDocumentOutputDeterministic()
    {
        var schema = new JsonObject { ["type"] = "string" };
        var builder = new AsyncApiDocumentBuilder(document => document.Info("Tests", "1.0.0"));
        builder.AddMessage<string>(() => new TextMessageDefinition(schema, "text"));
        var document = builder.Build();
        var initial = AsyncApiJson.Write(document);

        await Assert
            .That(initial.SequenceEqual(AsyncApiJson.Write(builder.Build())))
            .IsTrue();

        schema["type"] = "integer";

        await Assert
            .That(initial.SequenceEqual(AsyncApiJson.Write(document)))
            .IsTrue();
    }
}
