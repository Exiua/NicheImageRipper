using NicheImageRipper.Sdk.Interaction;

namespace NicheImageRipper.Tui;

/// <summary>Prints manual-action requests (e.g. solving a captcha) and waits for Enter.</summary>
internal sealed class ConsoleUserPrompt : IUserPrompt
{
    public async Task WaitForUserAsync(string message, CancellationToken cancellationToken = default)
    {
        TuiOutput.Write($"{message} Press Enter to continue...");
        await Task.Run(Console.ReadLine, cancellationToken);
    }
}
