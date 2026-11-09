using System.Text.Json.Serialization;
using Expresso.Api.Data;
using Expresso.Api.ErrorHandling;
using Expresso.Api.Features.Products;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<DomainExceptionHandler>();

builder.Services.AddDbContext<ExpressoDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("Expresso")));
builder.Services.AddScoped<ProductService>();

var app = builder.Build();

await app.InitializeDatabaseAsync();

app.UseExceptionHandler();
app.MapOpenApi();
app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "Expresso API"));
app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();

app.MapControllers();

app.Run();

// Exposed so integration tests can reference the entry point with WebApplicationFactory<Program>.
public partial class Program;
