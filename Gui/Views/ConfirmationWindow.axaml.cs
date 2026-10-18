using Avalonia.Controls;
using Avalonia.Interactivity;
using NicheImageRipper.Gui.Services.Shared;
using NicheImageRipper.Gui.ViewModels;

namespace NicheImageRipper.Gui.Views;

public partial class ConfirmationWindow : Window
{
    private ConfirmationViewModel ViewModel => (ConfirmationViewModel) DataContext!;
    
    public ConfirmationWindow()
    {
        InitializeComponent();
        TitleBarTheme.Attach(this);
    }

    private void Reject(object? sender, RoutedEventArgs e)
    {
        ViewModel.Confirmed = false;
        Close(ViewModel);
    }
    
    private void Accept(object? sender, RoutedEventArgs e)
    {
        ViewModel.Confirmed = true;
        Close(ViewModel);
    }
}