using System.Globalization;
using System.Windows;
using System.Windows.Markup;
using InventoryClient.Services;
using InventoryClient.ViewModels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace InventoryClient;

public partial class App : Application
{
    private readonly IHost _host;

    public App()
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            ContentRootPath = AppContext.BaseDirectory,
        });

        var baseUrl = builder.Configuration["Api:BaseUrl"]
            ?? throw new InvalidOperationException("Lipseste Api:BaseUrl din appsettings.json.");

        builder.Services.AddSingleton<UserSession>();
        builder.Services.AddSingleton<NavigationService>();
        builder.Services.AddSingleton<DialogService>();
        builder.Services.AddTransient<AuthHeaderHandler>();

        builder.Services.AddHttpClient<ApiClient>(client =>
        {
            client.BaseAddress = new Uri(baseUrl);
            client.Timeout = TimeSpan.FromSeconds(15);
        })
            .AddHttpMessageHandler<AuthHeaderHandler>();

        builder.Services.AddSingleton<MainViewModel>();
        builder.Services.AddTransient<LoginViewModel>();
        builder.Services.AddTransient<InventoryViewModel>();
        builder.Services.AddTransient<UsersViewModel>();
        builder.Services.AddSingleton<MainWindow>();

        _host = builder.Build();
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        FrameworkElement.LanguageProperty.OverrideMetadata(
            typeof(FrameworkElement),
            new FrameworkPropertyMetadata(XmlLanguage.GetLanguage(CultureInfo.CurrentCulture.IetfLanguageTag)));

        DispatcherUnhandledException += (_, args) =>
        {
            MessageBox.Show(args.Exception.Message, "Eroare neasteptata", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };

        await _host.StartAsync();
        _host.Services.GetRequiredService<MainWindow>().Show();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        await _host.StopAsync();
        _host.Dispose();
        base.OnExit(e);
    }
}