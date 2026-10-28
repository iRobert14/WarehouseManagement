using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InventoryClient.Services;
using InventoryCommon.Contracts;

namespace InventoryClient.ViewModels;

public partial class InventoryViewModel(ApiClient api, UserSession session, DialogService dialogs)
    : ObservableObject, ILoadable
{
    public const string AllCategories = "Toate categoriile";

    private CancellationTokenSource? _typingDelay;
    private CancellationTokenSource? _currentLoad;
    private bool _isRefreshingCategories;
    private bool _isReloadingList;

    public ObservableCollection<ProductDto> Products { get; } = [];
    public ObservableCollection<string> Categories { get; } = [AllCategories];
    public ObservableCollection<MovementItem> Movements { get; } = [];
    public ProductFormViewModel Form { get; } = new();

    public bool CanManageProducts => session.CanManageProducts;
    public bool CanDeleteProducts => session.CanDeleteProducts;

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

    [ObservableProperty]
    private bool _isEditorOpen;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PanelTitle), nameof(ShowStockSection), nameof(ShowDeleteButton))]
    private bool _isNewProduct;

    [ObservableProperty]
    private string? _formMessage;

    [ObservableProperty]
    private bool _formMessageIsError;

    [ObservableProperty]
    private string _stockAmountText = "1";

    [ObservableProperty]
    private string? _stockReason;

    public string PanelTitle => IsNewProduct ? "Produs nou" : "Detalii produs";
    public bool ShowStockSection => !IsNewProduct;
    public bool ShowDeleteButton => CanDeleteProducts && !IsNewProduct;

    public async Task LoadAsync()
    {
        await LoadCategoriesAsync();
        await LoadProductsAsync();
    }

    [RelayCommand]
    private Task RefreshAsync() => LoadAsync();

    [RelayCommand]
    private void NewProduct()
    {
        IsNewProduct = true;
        SelectedProduct = null;
        Form.Clear();
        Movements.Clear();
        FormMessage = null;
        IsEditorOpen = true;
    }

    [RelayCommand]
    private void CloseEditor()
    {
        IsNewProduct = false;
        SelectedProduct = null;
        IsEditorOpen = false;
        Form.Clear();
        Movements.Clear();
        FormMessage = null;
    }

    [RelayCommand]
    private async Task SaveProductAsync()
    {
        var error = Form.Validate();
        if (error is not null)
        {
            ShowFormMessage(error, isError: true);
            return;
        }

        try
        {
            var wasNew = Form.ProductId is null;
            var saved = Form.ProductId is { } id
                ? await api.UpdateProductAsync(id, Form.ToUpdateRequest())
                : await api.CreateProductAsync(Form.ToCreateRequest());

            IsNewProduct = false;
            Form.LoadFrom(saved);

            await LoadCategoriesAsync();
            await LoadProductsAsync(selectId: saved.Id);
            await LoadMovementsAsync(saved.Id);

            ShowFormMessage(wasNew ? "Produsul a fost adăugat." : "Modificările au fost salvate.", isError: false);
        }
        catch (ApiException ex)
        {
            ShowFormMessage(ex.Message, isError: true);
        }
    }

    [RelayCommand]
    private async Task DeleteProductAsync()
    {
        if (SelectedProduct is not { } product)
        {
            return;
        }

        var confirmed = dialogs.Confirm(
            $"Ștergi definitiv produsul „{product.Name}” ({product.Sku})?\nIstoricul lui de stoc se șterge și el.");
        if (!confirmed)
        {
            return;
        }

        try
        {
            await api.DeleteProductAsync(product.Id);
            CloseEditor();
            await LoadCategoriesAsync();
            await LoadProductsAsync();
        }
        catch (ApiException ex)
        {
            ShowFormMessage(ex.Message, isError: true);
        }
    }

    [RelayCommand]
    private Task StockInAsync() => AdjustStockAsync(+1);

    [RelayCommand]
    private Task StockOutAsync() => AdjustStockAsync(-1);

    private async Task AdjustStockAsync(int direction)
    {
        if (SelectedProduct is not { } product)
        {
            return;
        }

        if (!int.TryParse(StockAmountText.Trim(), out var amount) || amount <= 0)
        {
            ShowFormMessage("Cantitatea trebuie să fie un număr întreg mai mare ca 0.", isError: true);
            return;
        }

        try
        {
            await api.AdjustStockAsync(product.Id, direction * amount, StockReason);

            StockAmountText = "1";
            StockReason = null;

            await LoadProductsAsync(selectId: product.Id);
            await LoadMovementsAsync(product.Id);

            ShowFormMessage(direction > 0 ? $"Au intrat {amount} buc." : $"Au ieșit {amount} buc.", isError: false);
        }
        catch (ApiException ex)
        {
            ShowFormMessage(ex.Message, isError: true);
        }
    }

    partial void OnSelectedProductChanged(ProductDto? value)
    {
        if (_isReloadingList)
        {
            return;
        }

        if (value is null)
        {
            if (!IsNewProduct)
            {
                IsEditorOpen = false;
            }

            return;
        }

        var isDifferentProduct = Form.ProductId != value.Id;

        IsNewProduct = false;
        IsEditorOpen = true;

        if (isDifferentProduct)
        {
            Form.LoadFrom(value);
            FormMessage = null;
            StockAmountText = "1";
            StockReason = null;
            _ = LoadMovementsAsync(value.Id);
        }
    }

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

    private async Task LoadProductsAsync(Guid? selectId = null)
    {
        _currentLoad?.Cancel();
        var load = _currentLoad = new CancellationTokenSource();

        IsLoading = true;
        ErrorMessage = null;

        try
        {
            var category = SelectedCategory is null or AllCategories ? null : SelectedCategory;
            var products = await api.GetProductsAsync(SearchText, category, ShowLowStockOnly, load.Token);

            var idToSelect = selectId ?? SelectedProduct?.Id;

            _isReloadingList = true;
            Products.Clear();
            foreach (var product in products)
            {
                Products.Add(product);
            }
            _isReloadingList = false;

            SelectedProduct = Products.FirstOrDefault(p => p.Id == idToSelect);
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
            _isReloadingList = false;

            if (_currentLoad == load)
            {
                IsLoading = false;
            }
        }
    }

    private async Task LoadMovementsAsync(Guid productId)
    {
        try
        {
            var movements = await api.GetMovementsAsync(productId);

            Movements.Clear();
            foreach (var movement in movements)
            {
                Movements.Add(MovementItem.From(movement));
            }
        }
        catch (ApiException ex)
        {
            ShowFormMessage(ex.Message, isError: true);
        }
    }

    private void ShowFormMessage(string message, bool isError)
    {
        FormMessage = message;
        FormMessageIsError = isError;
    }

    private void UpdateSummary()
    {
        TotalProducts = Products.Count;
        TotalUnits = Products.Sum(p => p.Quantity);
        StockValue = Products.Sum(p => p.Price * p.Quantity);
        LowStockCount = Products.Count(p => p.IsLowStock);
    }
}