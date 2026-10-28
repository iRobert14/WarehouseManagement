namespace InventoryCommon.Contracts;

public record UserListItemDto(int Id, string Username, UserRole Role, bool IsActive, DateTime CreatedAtUtc);

public record CreateUserRequest(string Username, string Password, UserRole Role);

public record UpdateUserRequest(UserRole Role, bool IsActive);

public record ResetPasswordRequest(string NewPassword);