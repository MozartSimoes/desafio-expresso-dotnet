namespace Expresso.Api.Features.Auth;

/// <summary>Who can do what: attendants take orders, baristas move them along, managers run the menu.</summary>
public static class Roles
{
    public const string Attendant = "Attendant";
    public const string Barista = "Barista";
    public const string Manager = "Manager";

    // Combinations used in [Authorize(Roles = ...)]; the manager can also cover any station.
    public const string TakeOrders = Attendant + "," + Manager;
    public const string PrepareOrders = Barista + "," + Manager;
    public const string Staff = Attendant + "," + Barista + "," + Manager;
}
