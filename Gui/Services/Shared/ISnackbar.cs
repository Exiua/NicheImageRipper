using Avalonia.Controls.Notifications;

namespace Gui.Services.Shared;

public interface ISnackbar
{
    public void Show(string message, string title = "Snackbar", NotificationType type = NotificationType.Information,
                     long expirationMs = 3000);
}