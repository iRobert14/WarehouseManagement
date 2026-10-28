using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InventoryClient.Services;

namespace InventoryClient.ViewModels;

public partial class LoginViewModel(ApiClient api, UserSession session, NavigationService navigation) : ObservableObject
{
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoginCommand))]
    private string _username = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoginCommand))]
    private string _password = string.Empty;

    [ObservableProperty]
    private string? _errorMessage;

    private bool CanLogin() => !string.IsNullOrWhiteSpace(Username) && !string.IsNullOrEmpty(Password);

    [RelayCommand(CanExecute = nameof(CanLogin))]
    private async Task LoginAsync()
    {
        ErrorMessage = null;

        try
        {
            var response = await api.LoginAsync(Username.Trim(), Password);
            Password = string.Empty;
            session.Start(response);
            navigation.NavigateTo<InventoryViewModel>();
        }
        catch (ApiException ex)
        {
            ErrorMessage = ex.Message;
        }
    }
}