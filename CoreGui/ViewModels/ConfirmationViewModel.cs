namespace CoreGui.ViewModels;

public class ConfirmationViewModel : ViewModelBase
{
    public bool Confirmed { get; set; }
    public string Message { get; set; } = "";
}