using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InventoryClient.Services;

namespace InventoryClient.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly UserSession _session;

    public MainViewModel(NavigationService navigation, UserSession session)
    {
        Navigation = navigation;
        _session = session;

        _session.LoggedIn += (_, _) => RefreshUserInfo();
        _session.LoggedOut += (_, _) => Application.Current.Dispatcher.Invoke(() =>
        {
            RefreshUserInfo();
            Navigation.NavigateTo<LoginViewModel>();
        });

        Navigation.NavigateTo<LoginViewModel>();
    }

    public NavigationService Navigation { get; }

    public bool IsLoggedIn => _session.IsLoggedIn;

    public string UserDisplay => _session.CurrentUser is { } user
        ? $"{user.Username} · {user.Role}"
        : string.Empty;

    [RelayCommand]
    private void Logout() => _session.End();

    [RelayCommand]
    private void Minimize() => Application.Current.MainWindow.WindowState = WindowState.Minimized;

    [RelayCommand]
    private void Close() => Application.Current.Shutdown();

    private void RefreshUserInfo()
    {
        OnPropertyChanged(nameof(IsLoggedIn));
        OnPropertyChanged(nameof(UserDisplay));
    }
}