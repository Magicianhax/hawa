using System.Windows.Controls;

namespace Hawa.App.Settings.Pages;

public partial class BehaviourPage : UserControl
{
    public BehaviourPage(SettingsViewModel vm) { InitializeComponent(); DataContext = vm; }
}
