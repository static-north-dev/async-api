using System.Text.Json.Serialization;

namespace Producer;

[JsonConverter(typeof(JsonStringEnumConverter<OrderStatus>))]
internal enum OrderStatus
{
    Created,
    Confirmed
}
