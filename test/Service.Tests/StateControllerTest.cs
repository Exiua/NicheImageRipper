using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using NicheImageRipper.Service.Controllers;

namespace NicheImageRipper.Service.Tests;

public class StateControllerTest
{
    private readonly SavingRipper _ripper = new();
    private StateController Controller => new(NullLogger<StateController>.Instance, _ripper);

    [Fact]
    public async Task SaveRespondsOnlyAfterTheSaveFinishes()
    {
        var response = Controller.Save();
        Assert.False(response.IsCompleted);

        _ripper.FinishSave();

        Assert.IsType<OkResult>(await response);
    }

    [Fact]
    public async Task SaveCannotBeCancelledByTheRequest()
    {
        var response = Controller.Save();
        _ripper.FinishSave();
        await response;

        Assert.False(_ripper.SaveToken.CanBeCanceled);
    }

    [Fact]
    public async Task SaveErrorsReachTheCaller()
    {
        var response = Controller.Save();
        _ripper.FailSave(new IOException("disk gone"));

        await Assert.ThrowsAsync<IOException>(() => response);
    }

    private sealed class SavingRipper : NotImplementedRipperSingleton
    {
        private readonly TaskCompletionSource _save = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public CancellationToken SaveToken { get; private set; }

        public void FinishSave() => _save.SetResult();
        public void FailSave(Exception exception) => _save.SetException(exception);

        public override Task Save(CancellationToken cancellationToken = default)
        {
            SaveToken = cancellationToken;
            return _save.Task;
        }
    }
}
