using System.Text.Json.Serialization;
using Expresso.Api.Data;
using Expresso.Api.ErrorHandling;
using Expresso.Api.Features.Auth;
using Expresso.Api.Features.Menu;
using Expresso.Api.Features.Orders;
using Expresso.Api.Features.Products;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        // Invalid JSON gets a generic message instead of one that exposes internal type names.
        o.AllowInputFormatterExceptionMessages = false;
    });
builder.Services.AddOpenApi(o => o.AddBearerSecurityScheme());
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<DomainExceptionHandler>();
builder.Services.AddSimulatedAuth(builder.Configuration);

builder.Services.AddDbContext<ExpressoDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("Expresso")));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<ProductService>();
builder.Services.AddScoped<OrderService>();
builder.Services.AddMenuOfTheDay(builder.Configuration);

var app = builder.Build();

await app.InitializeDatabaseAsync();

app.UseExceptionHandler();
app.UseStatusCodePages(); // empty 401/403 responses also get a ProblemDetails body

// Swagger UI is static content with no endpoint; it must come before authorization, or the
// "login required by default" policy would block the documentation page itself.
app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "Expresso API"));

app.UseAuthentication();
app.UseAuthorization();

app.MapOpenApi().AllowAnonymous();
app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription().AllowAnonymous();

app.MapControllers();

app.Run();

// Exposed so integration tests can reference the entry point with WebApplicationFactory<Program>.
public partial class Program;
