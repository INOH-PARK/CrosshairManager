using CrosshairManager.Core.Settings;
using CrosshairManager.TestSupport;

// Test-only tool: SendHotkey <gesture> presses the chord with SendInput (TestSupport Input.SendChord) and exits 0.
// Exit 2: usage or unparsable gesture. Exit 1: SendInput did not accept every event.
if (args.Length != 1 || !HotkeyGesture.TryParse(args[0], out var gesture))
{
    Console.Error.WriteLine("usage: SendHotkey <gesture>   e.g. SendHotkey Ctrl+Alt+Shift+X");
    return 2;
}

try
{
    Input.SendChord(gesture);
}
catch (InvalidOperationException ex)
{
    Console.Error.WriteLine("SendHotkey: " + ex.Message);
    return 1;
}

Console.WriteLine("sent " + gesture);
return 0;
