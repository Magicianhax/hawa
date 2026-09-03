using System.Windows;

namespace Hawa.App.Settings;

public sealed class SettingsWindow : Window
{
    public SettingsWindow(AppServices services) { Title = "Hawa"; Width = 720; Height = 520; }
}
