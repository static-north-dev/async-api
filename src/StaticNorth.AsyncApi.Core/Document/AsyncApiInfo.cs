namespace StaticNorth.AsyncApi.Core;

/// <summary>Describes the API identified by an AsyncAPI document.</summary>
/// <param name="Title">The title of the API.</param>
/// <param name="Version">The version of the API described by the document.</param>
public sealed record AsyncApiInfo(string Title, string Version);
