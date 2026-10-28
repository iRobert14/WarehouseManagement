using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InventoryClient.Services;
using InventoryCommon.Contracts;

namespace InventoryClient.ViewModels;

public partial class InventoryViewModel(ApiClient api) : ObservableObject, ILoadable
{
    public const string AllCategories = "Toate categoriile";

    private CancellationTokenSource? _typingDelay;
    private CancellationTokenSource? _currentLoad;
    private bool _isRefreshingCategories;

    public ObservableCollection<ProductDto> Products { get; } = [];
    public ObservableCollection<string> Categories { get; } = [AllCategories];

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string? _selectedCategory = AllCategories;

    [ObservableProperty]
    private bool _showLowStockOnly;

    [ObservableProperty]
    private ProductDto? _selectedProduct;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private int _totalProducts;

    [ObservableProperty]
    private int _totalUnits;

    [ObservableProperty]
    private decimal _stockValue;

    [ObservableProperty]
    private int _lowStockCount;

    public async Task LoadAsync()
    {
        await LoadCategoriesAsync();
        await LoadProductsAsync();
    }

    [RelayCommand]
    private Task RefreshAsync() => LoadAsync();

    partial void OnSearchTextChanged(string value) => _ = LoadAfterTypingAsync();

    partial void OnSelectedCategoryChanged(string? value)
    {
        if (!_isRefreshingCategories)
        {
            _ = LoadProductsAsync();
        }
    }

    partial void OnShowLowStockOnlyChanged(bool value) => _ = LoadProductsAsync();

    // Asteapta 300 ms dupa ultima tasta, ca sa nu trimitem o cerere la fiecare litera.
    private async Task LoadAfterTypingAsync()
    {
        _typingDelay?.Cancel();
        _typingDelay = new CancellationTokenSource();

        try
        {
            await Task.Delay(300, _typingDelay.Token);
            await LoadProductsAsync();
        }
        catch (TaskCanceledException)
        {
        }
    }

    private async Task LoadCategoriesAsync()
    {
        try
        {
            var categories = await api.GetCategoriesAsync();
            var previous = SelectedCategory;

            _isRefreshingCategories = true;
            Categories.Clear();
            Categories.Add(AllCategories);
            foreach (var category in categories)
            {
                Categories.Add(category);
            }

            SelectedCategory = previous is not null && Categories.Contains(previous) ? previous : AllCategories;
        }
        catch (ApiException ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            _isRefreshingCategories = false;
        }
    }

    private async Task LoadProductsAsync()
    {
        _currentLoad?.Cancel();
        var load = _currentLoad = new CancellationTokenSource();

        IsLoading = true;
        ErrorMessage = null;

        try
        {
            var category = SelectedCategory is null or AllCategories ? null : SelectedCategory;
            var products = await api.GetProductsAsync(SearchText, category, ShowLowStockOnly, load.Token);

            var selectedId = SelectedProduct?.Id;
            Products.Clear();
            foreach (var product in products)
            {
                Products.Add(product);
            }

            SelectedProduct = Products.FirstOrDefault(p => p.Id == selectedId);
            UpdateSummary();
        }
        catch (OperationCanceledException)
        {
        }
        catch (ApiException ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            if (_currentLoad == load)
            {
                IsLoading = false;
            }
        }
    }

    private void UpdateSummary()
    {
        TotalProducts = Products.Count;
        TotalUnits = Products.Sum(p => p.Quantity);
        StockValue = Products.Sum(p => p.Price * p.Quantity);
        LowStockCount = Products.Count(p => p.IsLowStock);
    }
}