namespace NicheImageRipper.Gui.ViewModels;

public class ConfirmationViewModel : ViewModelBase
{
    public bool Confirmed { get; set; }
    public string Message { get; set; } = "";
    public string AcceptText { get; set; } = "Yes";
    public string RejectText { get; set; } = "No";

    /// <summary>False for a plain "continue" prompt that only needs acknowledging.</summary>
    public bool ShowReject { get; set; } = true;
}
