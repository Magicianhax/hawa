using System.Windows.Controls;

namespace Hawa.App.Settings.Pages;

public partial class NoiseControlPage : UserControl
{
    public NoiseControlPage(SettingsViewModel vm) { InitializeComponent(); DataContext = vm; }
}
