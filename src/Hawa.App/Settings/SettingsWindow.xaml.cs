using System.Windows.Controls;
using Hawa.App.Settings.Pages;

namespace Hawa.App.Settings;

public partial class SettingsWindow : Wpf.Ui.Controls.FluentWindow
{
    private readonly SettingsViewModel _vm;
    private readonly UserControl[] _pages;

    public SettingsWindow(AppServices services)
    {
        InitializeComponent();
        _vm = new SettingsViewModel(services);
        DataContext = _vm;
        _pages = new UserControl[] { new DevicePage(_vm), new NoiseControlPage(_vm), new BehaviourPage(_vm), new AboutPage(_vm) };
        PageHost.Content = _pages[0];
    }

    private void Nav_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Nav.SelectedIndex >= 0 && _pages is not null) PageHost.Content = _pages[Nav.SelectedIndex];
    }
}
