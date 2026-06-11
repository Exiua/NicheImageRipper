using System.Threading;
using System.Threading.Tasks;

namespace Gui.Services;

public interface IAsyncInitialization
{
    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}