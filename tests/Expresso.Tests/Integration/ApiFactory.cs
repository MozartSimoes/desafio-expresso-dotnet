using System.Net.Http.Headers;
using System.Net.Http.Json;
using Expresso.Api.Features.Auth;
using Expresso.Api.Features.Menu;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace Expresso.Tests.Integration;

/// <summary>
/// Runs the real API against a throwaway PostgreSQL container, so tests exercise the same database engine,
/// migrations and constraints as production. Requires Docker. External APIs are replaced by <see cref="FakeExternalApis"/>.
/// </summary>
public class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17").Build();

    public FakeExternalApis ExternalApis { get; } = new();

    public Task InitializeAsync() => _postgres.StartAsync();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Expresso", _postgres.GetConnectionString());
        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<IWeatherClient>(ExternalApis);
            services.AddSingleton<IHolidayClient>(ExternalApis);
        });
    }

    /// <summary>A client already logged in as one of the demo users (atendente, barista, gerente).</summary>
    public async Task<HttpClient> CreateClientAsAsync(string username)
    {
        var client = CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(username, $"{username}123"));
        response.EnsureSuccessStatusCode();
        var login = await response.Content.ReadFromJsonAsync<LoginResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login!.Token);
        return client;
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }
}

/// <summary>All integration test classes share one container; each test creates the data it needs.</summary>
[CollectionDefinition(Name)]
public class ApiCollection : ICollectionFixture<ApiFactory>
{
    public const string Name = "api";
}

/// <summary>Stands in for Open-Meteo and BrasilAPI; each test sets the answer or makes it fail.</summary>
public class FakeExternalApis : IWeatherClient, IHolidayClient
{
    public CurrentWeather? Weather { get; set; } = new(22, "céu limpo");
    public List<Holiday>? Holidays { get; set; } = [];

    public Task<CurrentWeather> GetCurrentAsync(double latitude, double longitude, CancellationToken ct) =>
        Weather is null ? throw new HttpRequestException("Open-Meteo fora do ar") : Task.FromResult(Weather);

    public Task<IReadOnlyList<Holiday>> GetNationalHolidaysAsync(int year, CancellationToken ct) =>
        Holidays is null
            ? throw new HttpRequestException("BrasilAPI fora do ar")
            : Task.FromResult<IReadOnlyList<Holiday>>(Holidays);
}
