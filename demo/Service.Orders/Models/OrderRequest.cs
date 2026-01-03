namespace Service.Orders.Models;

/// <summary>
/// Represents the payload submitted by the client when creating an order.
/// </summary>
public sealed record OrderRequest(string OrderId, string CustomerId, decimal Total);
