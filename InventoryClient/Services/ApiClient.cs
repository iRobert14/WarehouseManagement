using InventoryCommon.Contracts;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace InventoryClient.Services;

public class ApiException(string message) : Exception(message);

internal record ProblemResponse(string? Title, string? Detail, Dictionary<string, string[]>? Errors);

public class ApiClient(HttpClient http)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public Task<LoginResponse> LoginAsync(string username, string password) =>
        SendAsync<LoginResponse>(() => http.PostAsJsonAsync("api/auth/login", new LoginRequest(username, password), JsonOptions));

    public Task<List<ProductDto>> GetProductsAsync(string? search, string? category, bool lowStockOnly, CancellationToken cancellationToken = default)
    {
        var query = new List<string>();
        if (!string.IsNullOrWhiteSpace(search)) query.Add($"search={Uri.EscapeDataString(search.Trim())}");
        if (!string.IsNullOrWhiteSpace(category)) query.Add($"category={Uri.EscapeDataString(category)}");
        if (lowStockOnly) query.Add("lowStockOnly=true");

        var url = query.Count == 0 ? "api/products" : $"api/products?{string.Join('&', query)}";
        return SendAsync<List<ProductDto>>(() => http.GetAsync(url, cancellationToken));
    }

    public Task<List<string>> GetCategoriesAsync() =>
        SendAsync<List<string>>(() => http.GetAsync("api/products/categories"));

    public Task<ProductDto> CreateProductAsync(CreateProductRequest request) =>
        SendAsync<ProductDto>(() => http.PostAsJsonAsync("api/products", request, JsonOptions));

    public Task<ProductDto> UpdateProductAsync(Guid id, UpdateProductRequest request) =>
        SendAsync<ProductDto>(() => http.PutAsJsonAsync($"api/products/{id}", request, JsonOptions));

    public async Task DeleteProductAsync(Guid id)
    {
        using var response = await SendAsync(() => http.DeleteAsync($"api/products/{id}"));
    }

    public Task<ProductDto> AdjustStockAsync(Guid id, int delta, string? reason) =>
        SendAsync<ProductDto>(() => http.PostAsJsonAsync($"api/products/{id}/stock", new AdjustStockRequest(delta, reason), JsonOptions));

    public Task<List<StockMovementDto>> GetMovementsAsync(Guid id) =>
        SendAsync<List<StockMovementDto>>(() => http.GetAsync($"api/products/{id}/movements"));

    private static async Task<T> SendAsync<T>(Func<Task<HttpResponseMessage>> send)
    {
        using var response = await SendAsync(send);
        return await response.Content.ReadFromJsonAsync<T>(JsonOptions)
            ?? throw new ApiException("Serverul a trimis un raspuns gol.");
    }

    private static async Task<HttpResponseMessage> SendAsync(Func<Task<HttpResponseMessage>> send)
    {
        HttpResponseMessage response;
        try
        {
            response = await send();
        }
        catch (HttpRequestException)
        {
            throw new ApiException("Nu ma pot conecta la server. Verifica daca InventoryServer ruleaza.");
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            throw new ApiException("Serverul nu a raspuns la timp.");
        }

        if (response.IsSuccessStatusCode)
        {
            return response;
        }

        var message = await ReadErrorAsync(response);
        response.Dispose();
        throw new ApiException(message);
    }

    private static async Task<string> ReadErrorAsync(HttpResponseMessage response)
    {
        try
        {
            var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>(JsonOptions);

            if (problem?.Errors is { Count: > 0 } errors)
            {
                return string.Join(Environment.NewLine, errors.SelectMany(e => e.Value));
            }

            if (problem?.Detail is { Length: > 0 } detail)
            {
                return detail;
            }
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
        }

        return response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => "Sesiunea a expirat. Logheaza-te din nou.",
            HttpStatusCode.Forbidden => "Nu ai drepturi pentru aceasta actiune.",
            HttpStatusCode.NotFound => "Elementul nu mai exista.",
            _ => $"Eroare de la server ({(int)response.StatusCode}).",
        };
    }
}