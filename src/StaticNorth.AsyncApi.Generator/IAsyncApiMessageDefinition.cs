namespace StaticNorth.AsyncApi.Generator;

public interface IAsyncApiMessageDefinition<TMessage>
{
    void Configure(IMessageBuilder<TMessage> message);
}
