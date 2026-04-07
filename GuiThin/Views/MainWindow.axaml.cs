using Avalonia.Platform.Storage;
using GuiThin.ViewModels;
using ReactiveUI.Avalonia;

namespace GuiThin.Views;

public partial class MainWindow : ReactiveWindow<MainWindowViewModel>
{
    private static FilePickerFileType Json { get; } = new("JSON")
    {
        Patterns = ["*.json"],
        MimeTypes = ["application/json"],
    };
    
    public MainWindow()
    {
        ViewModel!.MainWindow = this;
        InitializeComponent();
    }
}