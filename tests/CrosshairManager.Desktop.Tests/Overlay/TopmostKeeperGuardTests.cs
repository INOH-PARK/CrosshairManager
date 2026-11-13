using CrosshairManager.Platform.Overlay;

namespace CrosshairManager.Desktop.Tests.Overlay;

/// <summary>
/// The overlay host is never shown or given content here, so no window becomes visible; only the 1 s timer check runs.
/// </summary>
[Trait("Category", "Desktop")]
public class TopmostKeeperGuardTests
{
    [Fact]
    public async Task Throwing_Checked_handler_is_reported_through_HandlerFailed_and_checks_continue()
    {
        using var host = new OverlayHost();
        using var keeper = new TopmostKeeper(host);
        var boom = new InvalidOperationException("boom");
        var reported = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
        var nextCheck = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var checks = 0;
        keeper.HandlerFailed += (_, ex) => reported.TrySetResult(ex);
        keeper.Checked += (_, _) =>
        {
            if (Interlocked.Increment(ref checks) == 1)
            {
                throw boom;
            }

            nextCheck.TrySetResult();
        };

        Assert.Same(boom, await reported.Task.WaitAsync(TimeSpan.FromSeconds(5)));
        await nextCheck.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(host.IsVisible);
    }
}
