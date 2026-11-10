using System.Globalization;

namespace Expresso.Web.Api;

/// <summary>How API values are shown to people: Portuguese names, money in reais, times in Brasília time.</summary>
public static class Labels
{
    private static readonly CultureInfo PtBr = new("pt-BR");
    private static readonly TimeZoneInfo Brasilia = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");

    public static string Status(string? status) => status switch
    {
        "Received" => "Recebido",
        "Preparing" => "Preparando",
        "Ready" => "Pronto",
        "Delivered" => "Entregue",
        "Canceled" => "Cancelado",
        null => "—",
        _ => status
    };

    public static string Receipt(string status) => status switch
    {
        "Pending" => "Pendente",
        "Issued" => "Emitido",
        "Failed" => "Falhou",
        _ => status
    };

    public static string Category(string category) => category switch
    {
        "Coffee" => "Cafés",
        "Tea" => "Chás",
        "ColdDrink" => "Bebidas geladas",
        "Snack" => "Salgados",
        "Dessert" => "Doces",
        _ => category
    };

    public static string Role(string? role) => role switch
    {
        "Attendant" => "Atendente",
        "Barista" => "Barista",
        "Manager" => "Gerente",
        _ => role ?? ""
    };

    /// <summary>The action that moves an order forward from this status, as the barista sees it.</summary>
    public static (string Next, string Button)? NextStep(string status) => status switch
    {
        "Received" => ("Preparing", "Iniciar preparo"),
        "Preparing" => ("Ready", "Marcar pronto"),
        "Ready" => ("Delivered", "Entregar"),
        _ => null
    };

    public static string Product(string name, string? size) => size is null ? name : $"{name} ({size})";
    public static string Money(decimal value) => value.ToString("C", PtBr);
    public static string Time(DateTimeOffset value) => TimeZoneInfo.ConvertTime(value, Brasilia).ToString("HH:mm", PtBr);
    public static string Clock(DateTimeOffset value) => TimeZoneInfo.ConvertTime(value, Brasilia).ToString("HH:mm:ss", PtBr);
    public static string DateTime(DateTimeOffset value) =>
        TimeZoneInfo.ConvertTime(value, Brasilia).ToString("dd/MM HH:mm:ss", PtBr);
}
