using CommunityToolkit.Mvvm.ComponentModel;
using InventoryClient.Services;

namespace InventoryClient.ViewModels;

public partial class InventoryViewModel(UserSession session) : ObservableObject
{
    public string WelcomeText =>
        $"Bine ai venit, {session.CurrentUser?.Username}! Rolul tau: {session.CurrentUser?.Role}";
}