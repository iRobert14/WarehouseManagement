using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;

namespace InventoryClient.Services;

public interface ILoadable
{
    Task LoadAsync();
}

public partial class NavigationService(IServiceProvider services) : ObservableObject
{
    [ObservableProperty]
    private ObservableObject? _currentViewModel;

    public void NavigateTo<TViewModel>() where TViewModel : ObservableObject
    {
        var viewModel = services.GetRequiredService<TViewModel>();
        CurrentViewModel = viewModel;

        if (viewModel is ILoadable loadable)
        {
            _ = loadable.LoadAsync();
        }
    }
}