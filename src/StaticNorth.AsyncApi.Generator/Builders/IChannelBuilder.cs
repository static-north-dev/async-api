using System.Text.Json.Nodes;

namespace StaticNorth.AsyncApi.Generator;

/// <summary>Configures a channel and its references to messages and servers.</summary>
public interface IChannelBuilder
{
    /// <summary>Sets the channel ID used by operations.</summary>
    /// <param name="id">The key under <c>channels</c>.</param>
    /// <returns>This builder.</returns>
    IChannelBuilder Name(string id);

    /// <summary>Sets the address or address template.</summary>
    /// <param name="address">The address, or <see langword="null"/> if the document does not specify one.</param>
    /// <returns>This builder.</returns>
    IChannelBuilder Address(string? address);

    /// <summary>Sets the channel description.</summary>
    /// <param name="description">The description to include in the document.</param>
    /// <returns>This builder.</returns>
    IChannelBuilder Description(string description);

    /// <summary>References a reusable message from this channel.</summary>
    /// <param name="messageId">The message component ID.</param>
    /// <param name="key">The channel-local message key. Defaults to the component ID.</param>
    /// <returns>This builder.</returns>
    IChannelBuilder Message(string messageId, string? key = null);

    /// <summary>References a server on which this channel is available.</summary>
    /// <param name="id">The ID of a server defined in the document.</param>
    /// <returns>This builder.</returns>
    IChannelBuilder Server(string id);

    /// <summary>Declares a parameter used in the channel address template.</summary>
    /// <param name="name">The parameter name inside the braces in the address.</param>
    /// <param name="parameter">The AsyncAPI Parameter Object to copy.</param>
    /// <returns>This builder.</returns>
    IChannelBuilder Parameter(string name, JsonObject parameter);

    /// <summary>Sets the protocol bindings for the channel.</summary>
    /// <param name="bindings">An object keyed by protocol name. The builder copies it.</param>
    /// <returns>This builder.</returns>
    IChannelBuilder Bindings(JsonObject bindings);

    /// <summary>Adds or replaces a specification extension on the channel.</summary>
    /// <param name="name">The extension name, starting with <c>x-</c>.</param>
    /// <param name="value">The JSON value to copy, or <see langword="null"/> to write JSON null.</param>
    /// <returns>This builder.</returns>
    IChannelBuilder WithExtension(string name, JsonNode? value);
}
