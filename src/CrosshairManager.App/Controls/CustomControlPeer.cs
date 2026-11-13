using System.Windows;
using System.Windows.Automation.Peers;

namespace CrosshairManager.App.Controls;

/// <summary>Makes a composite control a node of the UIA control view with a stable control type and class name.</summary>
internal sealed class CustomControlPeer(FrameworkElement owner, AutomationControlType controlType, string className) : FrameworkElementAutomationPeer(owner)
{
    protected override AutomationControlType GetAutomationControlTypeCore() => controlType;

    protected override string GetClassNameCore() => className;
}
