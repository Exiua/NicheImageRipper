using System.Threading;
using System.Threading.Tasks;

namespace NicheImageRipper.Gui.Services;

public interface IAsyncInitialization
{
    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}