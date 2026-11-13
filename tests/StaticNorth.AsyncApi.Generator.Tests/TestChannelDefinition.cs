namespace StaticNorth.AsyncApi.Generator.Tests;

internal sealed class TestChannelDefinition(
    string id = "test",
    string? message = null,
    string? key = null,
    string? address = null) : IAsyncApiChannelDefinition
{
    public void Configure(IChannelBuilder channel)
    {
        channel
            .Name(id)
            .Address(address);

        if (message is not null)
        {
            channel.Message(message, key);
        }
    }
}
