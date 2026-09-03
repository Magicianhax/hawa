using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

namespace Hawa.App.Settings.Pages;

public partial class DevicePage : UserControl
{
    private readonly SettingsViewModel _vm;
    public DevicePage(SettingsViewModel vm) { InitializeComponent(); _vm = vm; DataContext = vm; }

    private void OpenBluetoothSettings_Click(object sender, RoutedEventArgs e) =>
        Process.Start(new ProcessStartInfo("ms-settings:bluetooth") { UseShellExecute = true });

    private async void Refresh_Click(object sender, RoutedEventArgs e) => await _vm.RefreshDevicesAsync();
}
