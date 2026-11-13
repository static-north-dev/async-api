using System.Text.Json.Nodes;

namespace StaticNorth.AsyncApi.Generator;

/// <summary>Configures the host, protocol, and bindings of an AsyncAPI server.</summary>
public interface IServerBuilder
{
    /// <summary>Sets the server host.</summary>
    /// <param name="host">The hostname and optional port, without a protocol scheme.</param>
    /// <returns>This builder.</returns>
    IServerBuilder Host(string host);

    /// <summary>Sets the server's protocol and optional protocol version.</summary>
    /// <param name="protocol">The protocol name, such as <c>kafka</c>.</param>
    /// <param name="version">The protocol version, or <see langword="null"/> to omit it.</param>
    /// <returns>This builder.</returns>
    IServerBuilder Protocol(string protocol, string? version = null);

    /// <summary>Sets the server description.</summary>
    /// <param name="description">The description to include in the document.</param>
    /// <returns>This builder.</returns>
    IServerBuilder Description(string description);

    /// <summary>Sets the protocol bindings for the server.</summary>
    /// <param name="bindings">An object keyed by protocol name. The builder copies it.</param>
    /// <returns>This builder.</returns>
    IServerBuilder Bindings(JsonObject bindings);

    /// <summary>Adds or replaces a specification extension on the server.</summary>
    /// <param name="name">The extension name, starting with <c>x-</c>.</param>
    /// <param name="value">The JSON value to copy, or <see langword="null"/> to write JSON null.</param>
    /// <returns>This builder.</returns>
    IServerBuilder WithExtension(string name, JsonNode? value);
}
