using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using InventoryCommon.Contracts;

namespace InventoryClient.ViewModels;

public partial class ProductFormViewModel : ObservableObject
{
    public Guid? ProductId { get; private set; }

    [ObservableProperty]
    private string _sku = string.Empty;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _category = string.Empty;

    [ObservableProperty]
    private string _size = string.Empty;

    [ObservableProperty]
    private string _color = string.Empty;

    [ObservableProperty]
    private string _priceText = string.Empty;

    [ObservableProperty]
    private string _lowStockThresholdText = "5";

    [ObservableProperty]
    private string _initialQuantityText = "0";

    public void LoadFrom(ProductDto product)
    {
        ProductId = product.Id;
        Sku = product.Sku;
        Name = product.Name;
        Category = product.Category;
        Size = product.Size;
        Color = product.Color;
        PriceText = product.Price.ToString("0.00", CultureInfo.CurrentCulture);
        LowStockThresholdText = product.LowStockThreshold.ToString();
        InitialQuantityText = "0";
    }

    public void Clear()
    {
        ProductId = null;
        Sku = Name = Category = Size = Color = PriceText = string.Empty;
        LowStockThresholdText = "5";
        InitialQuantityText = "0";
    }

    public string? Validate()
    {
        if (string.IsNullOrWhiteSpace(Sku) || string.IsNullOrWhiteSpace(Name) ||
            string.IsNullOrWhiteSpace(Category) || string.IsNullOrWhiteSpace(Size) ||
            string.IsNullOrWhiteSpace(Color))
        {
            return "Completează SKU, nume, categorie, mărime și culoare.";
        }

        if (!TryParsePrice(PriceText, out var price) || price < 0)
        {
            return "Prețul nu este valid (exemplu: 49,90).";
        }

        if (!int.TryParse(LowStockThresholdText.Trim(), out var threshold) || threshold < 0)
        {
            return "Pragul de stoc redus trebuie să fie un număr întreg, minim 0.";
        }

        if (ProductId is null && (!int.TryParse(InitialQuantityText.Trim(), out var quantity) || quantity < 0))
        {
            return "Cantitatea inițială trebuie să fie un număr întreg, minim 0.";
        }

        return null;
    }

    public CreateProductRequest ToCreateRequest() => new(
        Sku, Name, Category, Size, Color, ParsePrice(PriceText),
        int.Parse(LowStockThresholdText.Trim()), int.Parse(InitialQuantityText.Trim()));

    public UpdateProductRequest ToUpdateRequest() => new(
        Sku, Name, Category, Size, Color, ParsePrice(PriceText),
        int.Parse(LowStockThresholdText.Trim()));

    // Accepta si "49,90", si "49.90".
    private static bool TryParsePrice(string text, out decimal value) =>
        decimal.TryParse(text.Trim().Replace(',', '.'), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out value);

    private static decimal ParsePrice(string text) =>
        TryParsePrice(text, out var value) ? value : 0;
}