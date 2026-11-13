namespace StaticNorth.AsyncApi.Generator.Tests;

internal sealed class OpaqueMessageDefinition : IAsyncApiMessageDefinition<object>
{
    public void Configure(IMessageBuilder<object> message)
    {
        message.Name("opaque");
    }
}
