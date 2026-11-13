using System.Text.Json;
using StaticNorth.AsyncApi.Core.Transport;

namespace StaticNorth.AsyncApi.Client.Transport;

/// <summary>Publishes and receives typed JSON payloads through an AsyncAPI transport.</summary>
public sealed class AsyncApiJsonClient
{
    private static readonly JsonSerializerOptions DefaultOptions = CreateDefaultOptions();
    private readonly IAsyncApiTransport _transport;
    private readonly JsonSerializerOptions _options;

    /// <summary>Creates a JSON client over the supplied transport.</summary>
    /// <param name="transport">The transport used for publishing and subscribing.</param>
    /// <param name="serializerOptions">Optional serialization settings. The client takes a copy when settings are supplied.</param>
    public AsyncApiJsonClient(IAsyncApiTransport transport, JsonSerializerOptions? serializerOptions = null)
    {
        ArgumentNullException.ThrowIfNull(transport);
        _transport = transport;
        _options = serializerOptions is null
            ? DefaultOptions
            : new JsonSerializerOptions(serializerOptions);
    }

    /// <summary>Resolves the concrete address to use for a channel.</summary>
    /// <param name="declaredAddress">The address or template declared in the contract.</param>
    /// <param name="channelAddress">An optional concrete address that takes precedence over the declared address.</param>
    /// <returns>The concrete address used for delivery.</returns>
    /// <exception cref="ArgumentException">The resulting address is missing, blank, or still contains a template.</exception>
    public static string ResolveAddress(string? declaredAddress, string? channelAddress)
    {
        var address = channelAddress ?? declaredAddress;

        if (string.IsNullOrWhiteSpace(address) || address.Contains('{') || address.Contains('}'))
        {
            throw new ArgumentException("Supply a concrete channel address for an absent or templated address.", nameof(channelAddress));
        }

        return address;
    }

    /// <summary>Serializes a message to UTF-8 JSON and publishes it.</summary>
    /// <typeparam name="T">The payload type.</typeparam>
    /// <param name="route">The route and content metadata for the message.</param>
    /// <param name="message">The payload to serialize.</param>
    /// <param name="allowsNull">Whether the contract permits a null payload.</param>
    /// <param name="cancellationToken">A token that requests cancellation of the publish.</param>
    /// <returns>A task that represents the transport's publish operation.</returns>
    public ValueTask PublishAsync<T>(AsyncApiMessageRoute route, T message, bool allowsNull, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(route);

        if (!allowsNull && message is null)
        {
            throw new ArgumentNullException(nameof(message));
        }

        var payload = JsonSerializer.SerializeToUtf8Bytes(message, _options);
        var envelope = AsyncApiEnvelope.FromOwnedPayload(route, payload);
        return _transport.PublishAsync(envelope, cancellationToken);
    }

    /// <summary>Publishes a message with no payload bytes.</summary>
    /// <param name="route">The route and content metadata for the message.</param>
    /// <param name="cancellationToken">A token that requests cancellation of the publish.</param>
    /// <returns>A task that represents the transport's publish operation.</returns>
    public ValueTask PublishAsync(AsyncApiMessageRoute route, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(route);
        return _transport.PublishAsync(route.GetEmptyEnvelope(), cancellationToken);
    }

    /// <summary>Registers a handler for deserialized JSON messages.</summary>
    /// <typeparam name="T">The expected payload type.</typeparam>
    /// <param name="route">The route and expected media type.</param>
    /// <param name="handler">The callback that receives the payload and a delivery cancellation token.</param>
    /// <param name="allowsNull">Whether the contract permits a null payload.</param>
    /// <param name="cancellationToken">A token that requests cancellation of subscription registration.</param>
    /// <returns>A handle to dispose when the subscription is no longer needed.</returns>
    /// <remarks>Delivery fails before the callback runs if the media type differs, the JSON cannot be deserialized, or the payload is unexpectedly null.</remarks>
    public ValueTask<IAsyncDisposable> SubscribeAsync<T>(
        AsyncApiMessageRoute route,
        Func<T, CancellationToken, ValueTask> handler,
        bool allowsNull,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(route);
        ArgumentNullException.ThrowIfNull(handler);
        cancellationToken.ThrowIfCancellationRequested();

        return _transport.SubscribeAsync(route, (envelope, token) =>
        {
            token.ThrowIfCancellationRequested();

            var actual = MediaType(envelope.Route.ContentType);
            var expected = MediaType(route.ContentType);

            if (!actual.Equals(expected, StringComparison.OrdinalIgnoreCase))
            {
                throw new JsonException($"Expected content type '{route.ContentType}', received '{envelope.Route.ContentType}'.");
            }

            var value = JsonSerializer.Deserialize<T>(envelope.Payload.Span, _options);

            if (value is null && !allowsNull)
            {
                throw new JsonException($"Message '{route.MessageKey}' requires a non-null payload.");
            }

            return handler(value!, token);
        }, cancellationToken);
    }

    /// <summary>Registers a handler for messages with no payload bytes.</summary>
    /// <param name="route">The route to subscribe to.</param>
    /// <param name="handler">The callback that receives a delivery cancellation token.</param>
    /// <param name="cancellationToken">A token that requests cancellation of subscription registration.</param>
    /// <returns>A handle to dispose when the subscription is no longer needed.</returns>
    /// <remarks>A delivery containing payload bytes fails before the callback runs.</remarks>
    public ValueTask<IAsyncDisposable> SubscribeAsync(
        AsyncApiMessageRoute route,
        Func<CancellationToken, ValueTask> handler,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(route);
        ArgumentNullException.ThrowIfNull(handler);
        cancellationToken.ThrowIfCancellationRequested();

        return _transport.SubscribeAsync(route, (envelope, token) =>
        {
            token.ThrowIfCancellationRequested();

            if (!envelope.Payload.IsEmpty)
            {
                throw new JsonException($"Message '{route.MessageKey}' declares no payload.");
            }

            return handler(token);
        }, cancellationToken);
    }

    private static ReadOnlySpan<char> MediaType(string contentType)
    {
        var separator = contentType.IndexOf(';', StringComparison.Ordinal);
        var value = separator < 0 ? contentType.AsSpan() : contentType.AsSpan(0, separator);
        return value.Trim();
    }

    private static JsonSerializerOptions CreateDefaultOptions()
    {
        var options = new JsonSerializerOptions { RespectNullableAnnotations = true };

        if (JsonSerializer.IsReflectionEnabledByDefault)
        {
            options.MakeReadOnly(populateMissingResolver: true);
        }

        return options;
    }
}
