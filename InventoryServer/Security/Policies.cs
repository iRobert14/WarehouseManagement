namespace InventoryServer.Security;

// Numele regulilor de acces folosite pe endpoint-uri
public static class Policies
{
    public const string ManageProducts = "ManageProducts"; // Admin + Manager
    public const string DeleteProducts = "DeleteProducts"; // doar Admin
}