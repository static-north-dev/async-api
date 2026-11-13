using System.Text.Json;
using Orders.Contracts;

var message = new OrderCreated { OrderId = Guid.Empty };
Console.WriteLine($"{PublishOrderCreatedMetadata.ChannelAddress}: {JsonSerializer.Serialize(message)}");
