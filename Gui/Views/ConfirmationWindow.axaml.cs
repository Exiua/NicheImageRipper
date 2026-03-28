using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Gui.ViewModels;

namespace Gui.Views;

public partial class ConfirmationWindow : Window
{
    private ConfirmationViewModel ViewModel => (ConfirmationViewModel) DataContext!;
    
    public ConfirmationWindow()
    {
        InitializeComponent();
    }

    private void Reject(object? sender, RoutedEventArgs e)
    {
        ViewModel.Confirmed = false;
        Close();
    }
    
    private void Accept(object? sender, RoutedEventArgs e)
    {
        ViewModel.Confirmed = true;
        Close();
    }
}