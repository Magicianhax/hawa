using System.Windows.Controls;

namespace Hawa.App.Settings.Pages;

public partial class AboutPage : UserControl
{
    public AboutPage(SettingsViewModel vm) { InitializeComponent(); DataContext = vm; }
}
