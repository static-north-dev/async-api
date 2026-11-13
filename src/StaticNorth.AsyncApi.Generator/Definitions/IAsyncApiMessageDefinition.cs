namespace StaticNorth.AsyncApi.Generator;

/// <summary>Defines a reusable message in a code-first AsyncAPI document.</summary>
/// <typeparam name="TMessage">The message payload type.</typeparam>
public interface IAsyncApiMessageDefinition<TMessage>
{
    /// <summary>Configures the reusable message during document construction.</summary>
    /// <param name="message">The builder for the reusable message.</param>
    void Configure(IMessageBuilder<TMessage> message);
}
