using System.Collections;
using CrosshairManager.Cli;
using CrosshairManager.Platform.Dpi;

DpiAwareness.EnsurePerMonitorV2();
return CliApp.Run(args, Console.Out, Console.Error, Environment.GetEnvironmentVariables());
