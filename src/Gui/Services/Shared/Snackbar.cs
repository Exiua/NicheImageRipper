using System;
using Avalonia.Controls.Notifications;

namespace NicheImageRipper.Gui.Services.Shared;

public class Snackbar(WindowNotificationManager manager) : ISnackbar
{
    public void Show(string message, string title = "Snackbar", NotificationType type = NotificationType.Information, long expirationMs = 3000)
    {
        manager.Show(new Notification(
            title,
            message,
            NotificationType.Information,
            TimeSpan.FromMilliseconds(expirationMs)
        ));
    }
}