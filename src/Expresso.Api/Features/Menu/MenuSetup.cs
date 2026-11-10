using Microsoft.Extensions.Http.Resilience;

namespace Expresso.Api.Features.Menu;

public static class MenuSetup
{
    public static IServiceCollection AddMenuOfTheDay(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<CafeteriaOptions>(configuration.GetSection("Cafeteria"));
        services.AddMemoryCache();
        services.AddScoped<MenuService>();

        services.AddHttpClient<IWeatherClient, OpenMeteoClient>(c => c.BaseAddress = new Uri("https://api.open-meteo.com/"))
            .AddStandardResilienceHandler(ConfigureExternalApi);
        services.AddHttpClient<IHolidayClient, BrasilApiHolidayClient>(c => c.BaseAddress = new Uri("https://brasilapi.com.br/"))
            .AddStandardResilienceHandler(ConfigureExternalApi);

        return services;
    }

    // A customer at the counter will not wait: short timeouts, one quick retry, and a circuit breaker that
    // stops calling an API that keeps failing, so a slow API cannot slow down the menu.
    private static void ConfigureExternalApi(HttpStandardResilienceOptions options)
    {
        options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(2);
        options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(5);
        options.Retry.MaxRetryAttempts = 1;
        options.Retry.Delay = TimeSpan.FromMilliseconds(300);
    }
}
