namespace Expresso.Api.Features.Receipts;

public static class ReceiptsSetup
{
    public static IServiceCollection AddFiscalReceipts(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<FiscalReceiptOptions>(configuration.GetSection(FiscalReceiptOptions.Section));
        services.Configure<FiscalEmitterOptions>(configuration.GetSection(FiscalEmitterOptions.Section));
        services.AddSingleton<IFiscalEmitter, SimulatedFiscalEmitter>();
        services.AddScoped<ReceiptService>();
        services.AddHostedService<FiscalReceiptWorker>();
        return services;
    }
}
