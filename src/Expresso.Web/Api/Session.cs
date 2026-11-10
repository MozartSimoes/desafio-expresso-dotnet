namespace Expresso.Web.Api;

/// <summary>
/// Who is logged in on this browser tab. Blazor Server keeps one instance per connection (scoped), so the
/// JWT stays on the server and never reaches the browser's storage. Reloading the page means logging in again.
/// </summary>
public class Session
{
    public string? Token { get; private set; }
    public string? Name { get; private set; }
    public string? Role { get; private set; }

    public bool IsLoggedIn => Token is not null;
    public bool IsManager => Role == "Manager";

    // Same rules as the API; here they only decide which buttons to show. The API still enforces them.
    public bool CanTakeOrders => Role is "Attendant" or "Manager";
    public bool CanPrepareOrders => Role is "Barista" or "Manager";

    public event Action? Changed;

    public void SignIn(LoginResponse login)
    {
        (Token, Name, Role) = (login.Token, login.Name, login.Role);
        Changed?.Invoke();
    }

    public void SignOut()
    {
        (Token, Name, Role) = (null, null, null);
        Changed?.Invoke();
    }
}
