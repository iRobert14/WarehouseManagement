using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InventoryClient.Services;
using InventoryCommon;
using InventoryCommon.Contracts;

namespace InventoryClient.ViewModels;

public partial class UsersViewModel(ApiClient api, UserSession session) : ObservableObject, ILoadable
{
    private bool _isReloadingList;

    public ObservableCollection<UserListItemDto> Users { get; } = [];
    public IReadOnlyList<UserRole> Roles { get; } = Enum.GetValues<UserRole>();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEditingSelf), nameof(CanEditRoleAndStatus))]
    private UserListItemDto? _selectedUser;

    [ObservableProperty]
    private bool _isEditorOpen;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PanelTitle), nameof(IsExistingUser), nameof(PasswordLabel))]
    private bool _isNewUser;

    [ObservableProperty]
    private string _username = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private UserRole _role = UserRole.Employee;

    [ObservableProperty]
    private bool _isActive = true;

    [ObservableProperty]
    private string? _message;

    [ObservableProperty]
    private bool _messageIsError;

    [ObservableProperty]
    private string? _errorMessage;

    public string PanelTitle => IsNewUser ? "Utilizator nou" : "Editare utilizator";
    public string PasswordLabel => IsNewUser ? "Parolă" : "Parolă nouă (pentru resetare)";
    public bool IsExistingUser => !IsNewUser;
    public bool IsEditingSelf => SelectedUser is not null && SelectedUser.Id == session.CurrentUser?.Id;
    public bool CanEditRoleAndStatus => !IsEditingSelf;

    public Task LoadAsync() => LoadUsersAsync();

    [RelayCommand]
    private Task RefreshAsync() => LoadUsersAsync();

    [RelayCommand]
    private void NewUser()
    {
        IsNewUser = true;
        SelectedUser = null;
        Username = string.Empty;
        Password = string.Empty;
        Role = UserRole.Employee;
        IsActive = true;
        Message = null;
        IsEditorOpen = true;
    }

    [RelayCommand]
    private void CloseEditor()
    {
        IsNewUser = false;
        SelectedUser = null;
        IsEditorOpen = false;
        Password = string.Empty;
        Message = null;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        try
        {
            if (IsNewUser)
            {
                var created = await api.CreateUserAsync(new CreateUserRequest(Username, Password, Role));
                Password = string.Empty;
                IsNewUser = false;
                await LoadUsersAsync(selectId: created.Id);
                ShowMessage($"Contul „{created.Username}” a fost creat.", isError: false);
            }
            else if (SelectedUser is { } user)
            {
                await api.UpdateUserAsync(user.Id, new UpdateUserRequest(Role, IsActive));
                await LoadUsersAsync(selectId: user.Id);
                ShowMessage("Modificările au fost salvate.", isError: false);
            }
        }
        catch (ApiException ex)
        {
            ShowMessage(ex.Message, isError: true);
        }
    }

    [RelayCommand]
    private async Task ResetPasswordAsync()
    {
        if (SelectedUser is not { } user)
        {
            return;
        }

        if (string.IsNullOrEmpty(Password))
        {
            ShowMessage("Scrie parola nouă în câmpul de parolă.", isError: true);
            return;
        }

        try
        {
            await api.ResetPasswordAsync(user.Id, Password);
            Password = string.Empty;
            ShowMessage($"Parola pentru „{user.Username}” a fost schimbată.", isError: false);
        }
        catch (ApiException ex)
        {
            ShowMessage(ex.Message, isError: true);
        }
    }

    partial void OnSelectedUserChanged(UserListItemDto? value)
    {
        if (_isReloadingList)
        {
            return;
        }

        if (value is null)
        {
            if (!IsNewUser)
            {
                IsEditorOpen = false;
            }

            return;
        }

        IsNewUser = false;
        Username = value.Username;
        Role = value.Role;
        IsActive = value.IsActive;
        Password = string.Empty;
        Message = null;
        IsEditorOpen = true;
    }

    private async Task LoadUsersAsync(int? selectId = null)
    {
        ErrorMessage = null;

        try
        {
            var users = await api.GetUsersAsync();
            var idToSelect = selectId ?? SelectedUser?.Id;

            _isReloadingList = true;
            Users.Clear();
            foreach (var user in users)
            {
                Users.Add(user);
            }
            _isReloadingList = false;

            SelectedUser = Users.FirstOrDefault(u => u.Id == idToSelect);
        }
        catch (ApiException ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            _isReloadingList = false;
        }
    }

    private void ShowMessage(string message, bool isError)
    {
        Message = message;
        MessageIsError = isError;
    }
}