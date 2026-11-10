using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

namespace Expresso.Api.Features.Auth;

public static class AuthSetup
{
    public static IServiceCollection AddSimulatedAuth(this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection("Auth");
        services.Configure<AuthOptions>(section);
        var auth = section.Get<AuthOptions>() ?? new AuthOptions();

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(o =>
            {
                // Keep the claim names exactly as in the token ("role", "name") instead of the legacy
                // WS-Federation URIs; otherwise the role checks below would look for the wrong claim.
                o.MapInboundClaims = false;
                o.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = auth.Issuer,
                    ValidAudience = auth.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(auth.SigningKey)),
                    NameClaimType = "name",
                    RoleClaimType = "role"
                };
            });
        // Secure by default: every endpoint requires a logged-in user unless it is marked [AllowAnonymous].
        services.AddAuthorization(o => o.FallbackPolicy = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .Build());

        return services;
    }

    /// <summary>Adds the "Authorize" button to Swagger, so the API can be tried with a token from /api/auth/login.</summary>
    public static void AddBearerSecurityScheme(this Microsoft.AspNetCore.OpenApi.OpenApiOptions options) =>
        options.AddDocumentTransformer((document, _, _) =>
        {
            document.Components ??= new OpenApiComponents();
            document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
            document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Description = "Faça login em POST /api/auth/login e cole aqui o token."
            };
            document.Security = [new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference("Bearer", document)] = [] }];
            return Task.CompletedTask;
        });
}
