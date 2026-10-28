using InventoryCommon;
using InventoryCommon.Contracts;

namespace InventoryClient.Services;

public class UserSession
{
    public UserDto? CurrentUser { get; private set; }
    public string? AccessToken { get; private set; }

    public bool IsLoggedIn => AccessToken is not null;
    public bool CanManageProducts => CurrentUser?.Role is UserRole.Admin or UserRole.Manager;
    public bool CanDeleteProducts => CurrentUser?.Role is UserRole.Admin;

    public event EventHandler? LoggedIn;
    public event EventHandler? LoggedOut;

    public void Start(LoginResponse login)
    {
        CurrentUser = login.User;
        AccessToken = login.AccessToken;
        LoggedIn?.Invoke(this, EventArgs.Empty);
    }

    public void End()
    {
        if (AccessToken is null)
        {
            return;
        }

        CurrentUser = null;
        AccessToken = null;
        LoggedOut?.Invoke(this, EventArgs.Empty);
    }
}