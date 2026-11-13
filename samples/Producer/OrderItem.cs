namespace Producer;

internal sealed record OrderItem(string Sku, decimal Price, OrderItem? Replacement = null);
