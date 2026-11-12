using System.Runtime.CompilerServices;
using CrosshairManager.TestSupport;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace CrosshairManager.Ui.Tests;

internal static class AssemblySetup
{
    [ModuleInitializer]
    internal static void Initialize() => DpiAwareness.EnsurePerMonitorV2();
}
