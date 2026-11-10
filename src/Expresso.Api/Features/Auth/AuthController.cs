using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Expresso.Api.Features.Auth;

public record LoginRequest(
    [Required(ErrorMessage = "Informe o usuário.")] string Username,
    [Required(ErrorMessage = "Informe a senha.")] string Password);

public record LoginResponse(string Token, string Name, string Role, DateTimeOffset ExpiresAt);

[ApiController]
[Route("api/auth")]
[AllowAnonymous]
public class AuthController(IOptions<AuthOptions> options, TimeProvider clock) : ControllerBase
{
    /// <summary>
    /// Simulated login with the demo users (atendente, barista, gerente). Returns a JWT to send as
    /// "Authorization: Bearer {token}"; in Swagger, use the Authorize button.
    /// </summary>
    [HttpPost("login")]
    [ProducesResponseType<LoginResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public ActionResult<LoginResponse> Login(LoginRequest request)
    {
        var auth = options.Value;
        var user = auth.Users.FirstOrDefault(u =>
            string.Equals(u.Username, request.Username, StringComparison.OrdinalIgnoreCase) &&
            u.Password == request.Password);

        // Same answer for unknown user and wrong password, so the response does not reveal which users exist.
        if (user is null)
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Usuário ou senha inválidos.");

        var expiresAt = clock.GetUtcNow().AddHours(auth.TokenLifetimeHours);
        var token = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = auth.Issuer,
            Audience = auth.Audience,
            Expires = expiresAt.UtcDateTime,
            Subject = new ClaimsIdentity(
            [
                new Claim("sub", user.Username),
                new Claim("name", user.Name),
                new Claim("role", user.Role)
            ]),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(auth.SigningKey)), SecurityAlgorithms.HmacSha256)
        });

        return new LoginResponse(token, user.Name, user.Role, expiresAt);
    }
}
