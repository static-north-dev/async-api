namespace StaticNorth.AsyncApi.Generator;

/// <summary>Defines a channel in a code-first AsyncAPI document.</summary>
public interface IAsyncApiChannelDefinition
{
    /// <summary>Configures the channel during document construction.</summary>
    /// <param name="channel">The builder for the channel.</param>
    void Configure(IChannelBuilder channel);
}
