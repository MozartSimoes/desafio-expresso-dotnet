using Expresso.Web.Api;
using Microsoft.AspNetCore.Components;

namespace Expresso.Web.Components.Shared;

/// <summary>Base for pages that need a logged-in user: sends visitors to the login screen and shows API errors.</summary>
public abstract class StaffPage : ComponentBase
{
    [Inject] protected Session Session { get; set; } = null!;
    [Inject] protected ExpressoApi Api { get; set; } = null!;
    [Inject] protected NavigationManager Nav { get; set; } = null!;

    /// <summary>Message from the last failed API call, shown at the top of the page.</summary>
    protected string? Error { get; set; }

    protected override async Task OnInitializedAsync()
    {
        if (!Session.IsLoggedIn)
        {
            Nav.NavigateTo("");
            return;
        }
        await RunAsync(LoadAsync);
    }

    protected virtual Task LoadAsync() => Task.CompletedTask;

    /// <summary>Runs an API call and turns its error into the page's message instead of crashing the page.</summary>
    protected async Task<bool> RunAsync(Func<Task> action)
    {
        Error = null;
        try
        {
            await action();
            return true;
        }
        catch (ApiException ex)
        {
            Error = ex.Message;
            return false;
        }
    }
}
