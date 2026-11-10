using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Expresso.Api.Domain;
using Expresso.Api.Features.Auth;
using Expresso.Api.Features.Menu;
using Expresso.Api.Features.Receipts;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
    public FakeFiscalEmitter FiscalEmitter { get; } = new();
    public LogCollector Logs { get; } = new();

    public Task InitializeAsync() => _postgres.StartAsync();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Expresso", _postgres.GetConnectionString());

        // Fast receipt worker, so tests see retries in milliseconds instead of seconds.
        builder.UseSetting("FiscalReceipts:PollInterval", "00:00:00.100");
        builder.UseSetting("FiscalReceipts:EmitTimeout", "00:00:00.500");
        builder.UseSetting("FiscalReceipts:RetryBaseDelay", "00:00:00.050");
        builder.UseSetting("FiscalReceipts:MaxAttempts", "3");

        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<IWeatherClient>(ExternalApis);
            services.AddSingleton<IHolidayClient>(ExternalApis);
            services.AddSingleton<IFiscalEmitter>(FiscalEmitter);
            services.AddSingleton<ILoggerProvider>(Logs);
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

/// <summary>Keeps every log line the API writes, so tests can check what the team would find in the logs.</summary>
public class LogCollector : ILoggerProvider
{
    private readonly ConcurrentQueue<string> _lines = new();

    public IReadOnlyCollection<string> Lines => _lines;
    public ILogger CreateLogger(string categoryName) => new Collector(_lines);
    public void Dispose() { }

    private sealed class Collector(ConcurrentQueue<string> lines) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => lines.Enqueue($"{logLevel}: {formatter(state, exception)}");
    }
}

/// <summary>Stands in for the fiscal emitter: issues at once, unless a test makes it fail or hang for an order.</summary>
public class FakeFiscalEmitter : IFiscalEmitter
{
    private readonly ConcurrentDictionary<int, int> _failuresLeft = new();
    private readonly ConcurrentDictionary<int, bool> _hanging = new();

    public void FailNext(int orderId, int times) => _failuresLeft[orderId] = times;
    public void Hang(int orderId, bool hang = true) => _hanging[orderId] = hang;

    public async Task<string> EmitAsync(int receiptId, Order order, CancellationToken ct)
    {
        if (_hanging.GetValueOrDefault(order.Id))
            await Task.Delay(Timeout.Infinite, ct);

        if (_failuresLeft.GetValueOrDefault(order.Id) > 0)
        {
            _failuresLeft[order.Id]--;
            throw new FiscalEmitterException("Emissor fora do ar (teste)");
        }

        return $"TESTE {receiptId}";
    }
}
