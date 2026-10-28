using System.Windows;

namespace InventoryClient.Services;

public class DialogService
{
    public bool Confirm(string message, string title = "Confirmare") =>
        MessageBox.Show(
            Application.Current.MainWindow,
            message,
            title,
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No) == MessageBoxResult.Yes;
}