using System.Threading.Tasks;

namespace Gui.Services;

public interface IAsyncInitialization
{
    Task InitializeAsync()
    {
        return Task.CompletedTask;
    }
}