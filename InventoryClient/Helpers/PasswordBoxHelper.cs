using System.Windows;
using System.Windows.Controls;

namespace InventoryClient.Helpers;

public static class PasswordBoxHelper
{
    public static readonly DependencyProperty BoundPasswordProperty =
        DependencyProperty.RegisterAttached(
            "BoundPassword",
            typeof(string),
            typeof(PasswordBoxHelper),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnBoundPasswordChanged));

    public static string? GetBoundPassword(DependencyObject element) =>
        (string?)element.GetValue(BoundPasswordProperty);

    public static void SetBoundPassword(DependencyObject element, string? value) =>
        element.SetValue(BoundPasswordProperty, value);

    private static void OnBoundPasswordChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not PasswordBox box)
        {
            return;
        }

        box.PasswordChanged -= OnPasswordChanged;

        var newPassword = e.NewValue as string ?? string.Empty;
        if (box.Password != newPassword)
        {
            box.Password = newPassword;
        }

        box.PasswordChanged += OnPasswordChanged;
    }

    private static void OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        var box = (PasswordBox)sender;
        SetBoundPassword(box, box.Password);
    }
}