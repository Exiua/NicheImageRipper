using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using NicheImageRipper.Gui.ViewModels;
using NicheImageRipper.Gui.Views;
using NicheImageRipper.Sdk.Interaction;

namespace NicheImageRipper.Gui.Services.Shared;

/// <summary>Shows manual-action requests (e.g. solving a captcha) as a dialog with a single Continue button.</summary>
public sealed class GuiUserPrompt : IUserPrompt
{
    public Task WaitForUserAsync(string message, CancellationToken cancellationToken = default)
    {
        return Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var dialog = new ConfirmationWindow
            {
                Title = "Action needed",
                DataContext = new ConfirmationViewModel
                {
                    Message = message,
                    AcceptText = "Continue",
                    ShowReject = false,
                },
            };

            await using var registration = cancellationToken.Register(() => Dispatcher.UIThread.Post(dialog.Close));
            var owner = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
            if (owner is not null)
            {
                await dialog.ShowDialog<ConfirmationViewModel?>(owner);
                return;
            }

            var closed = new TaskCompletionSource();
            dialog.Closed += (_, _) => closed.TrySetResult();
            dialog.Show();
            await closed.Task;
        });
    }
}
