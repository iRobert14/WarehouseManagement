using InventoryServer.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ---------- Baza de date ----------
var connectionString = builder.Configuration.GetConnectionString("Inventory")
    ?? throw new InvalidOperationException(
        "Lipseste connection string-ul 'Inventory'. Adauga-l cu click dreapta pe InventoryServer -> Manage User Secrets.");

builder.Services.AddDbContext<InventoryDbContext>(options =>
    options.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 4, 0))));

builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// ---------- Endpoint-uri de test ----------
app.MapGet("/", () => "Inventory API is running");

app.MapGet("/api/health/db", async (InventoryDbContext db) =>
    await db.Database.CanConnectAsync()
        ? Results.Ok("Conexiunea la baza de date merge!")
        : Results.Problem("Nu ma pot conecta la baza de date."));

app.Run();