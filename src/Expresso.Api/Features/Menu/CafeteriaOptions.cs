namespace Expresso.Api.Features.Menu;

/// <summary>Where the cafeteria is and when it opens; bound from the "Cafeteria" section of appsettings.</summary>
public class CafeteriaOptions
{
    public string City { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string TimeZone { get; set; } = "America/Sao_Paulo";
    public string RegularHours { get; set; } = string.Empty;
    public string HolidayHours { get; set; } = string.Empty;
}
