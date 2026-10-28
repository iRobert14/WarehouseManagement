namespace InventoryCommon.Contracts;

public record LoginRequest(string Username, string Password);

public record UserDto(int Id, string Username, UserRole Role);

public record LoginResponse(string AccessToken, DateTime ExpiresAtUtc, UserDto User);