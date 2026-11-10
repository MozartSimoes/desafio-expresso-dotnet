namespace Expresso.Api.Domain;

public enum OrderStatus
{
    Received,
    Preparing,
    Ready,
    Delivered,
    Canceled
}

public static class OrderStatusExtensions
{
    /// <summary>Name shown to people in error messages; the API itself uses the enum names.</summary>
    public static string ToLabel(this OrderStatus status) => status switch
    {
        OrderStatus.Received => "Recebido",
        OrderStatus.Preparing => "Preparando",
        OrderStatus.Ready => "Pronto",
        OrderStatus.Delivered => "Entregue",
        OrderStatus.Canceled => "Cancelado",
        _ => status.ToString()
    };
}
