namespace Expresso.Api.Features.Auth;

/// <summary>
/// Simulated login, as agreed for this challenge: fixed demo users in configuration. In production the token
/// would come from the company's identity provider; the [Authorize] rules on the endpoints would not change.
/// </summary>
public class AuthOptions
{
    public string Issuer { get; set; } = "expresso-api";
    public string Audience { get; set; } = "expresso-api";

    /// <summary>Local development value only; in production it comes from a secret store or environment variable.</summary>
    public string SigningKey { get; set; } = string.Empty;

    public int TokenLifetimeHours { get; set; } = 8;
    public List<DemoUser> Users { get; set; } = [];
}

public class DemoUser
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
}
