using Microsoft.AspNetCore.Mvc;

namespace Expresso.Api.Features.Menu;

[ApiController]
[Route("api/menu")]
public class MenuController(MenuService service) : ControllerBase
{
    /// <summary>
    /// Menu of the day: drinks suggested for the current weather (Open-Meteo) and holiday opening hours
    /// (BrasilAPI). Always answers; if an external API is unavailable, that part is left out with a warning.
    /// </summary>
    [HttpGet("today")]
    public Task<MenuTodayResponse> Today(CancellationToken ct) => service.GetTodayAsync(ct);
}
