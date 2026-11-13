using System.Text.Json.Nodes;

namespace StaticNorth.AsyncApi.Generator;

/// <summary>Configures an operation, its direction, and its channel and message references.</summary>
public interface IOperationBuilder
{
    /// <summary>Sets the operation ID.</summary>
    /// <param name="id">The key under <c>operations</c>.</param>
    /// <returns>This builder.</returns>
    IOperationBuilder Name(string id);

    /// <summary>Selects the channel used by the operation.</summary>
    /// <param name="id">The ID of a channel defined in the document.</param>
    /// <returns>This builder.</returns>
    IOperationBuilder Channel(string id);

    /// <summary>Declares that the application described by the document sends messages.</summary>
    /// <returns>This builder.</returns>
    IOperationBuilder Send();

    /// <summary>Declares that the application described by the document receives messages.</summary>
    /// <returns>This builder.</returns>
    IOperationBuilder Receive();

    /// <summary>Sets the operation description.</summary>
    /// <param name="description">The description to include in the document.</param>
    /// <returns>This builder.</returns>
    IOperationBuilder Description(string description);

    /// <summary>Limits the operation to a message on its channel.</summary>
    /// <param name="channelMessageKey">The key declared on the channel for this message.</param>
    /// <returns>This builder.</returns>
    IOperationBuilder Message(string channelMessageKey);

    /// <summary>References a reusable operation trait.</summary>
    /// <param name="id">The component ID of the operation trait.</param>
    /// <returns>This builder.</returns>
    IOperationBuilder Trait(string id);

    /// <summary>Sets the protocol bindings for the operation.</summary>
    /// <param name="bindings">An object keyed by protocol name. The builder copies it.</param>
    /// <returns>This builder.</returns>
    IOperationBuilder Bindings(JsonObject bindings);

    /// <summary>Adds or replaces a specification extension on the operation.</summary>
    /// <param name="name">The extension name, starting with <c>x-</c>.</param>
    /// <param name="value">The JSON value to copy, or <see langword="null"/> to write JSON null.</param>
    /// <returns>This builder.</returns>
    IOperationBuilder WithExtension(string name, JsonNode? value);
}
