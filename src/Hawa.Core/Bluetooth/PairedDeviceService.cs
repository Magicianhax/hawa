using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;

namespace Hawa.Core.Bluetooth;

public sealed record PairedDeviceInfo(string Id, string Name);

/// <summary>Finds the paired AirPods (Bluetooth Classic) and tracks its connection status.</summary>
public sealed class PairedDeviceService : IDisposable
{
    private BluetoothDevice? _device;

    public bool IsConnected { get; private set; }
    public string? DeviceName { get; private set; }
    public string? DeviceId { get; private set; }
    public event Action<bool>? ConnectionChanged;

    public static async Task<IReadOnlyList<PairedDeviceInfo>> FindAirPodsAsync()
    {
        var selector = BluetoothDevice.GetDeviceSelectorFromPairingState(true);
        var infos = await DeviceInformation.FindAllAsync(selector);
        return infos
            .Where(i => i.Name.Contains("AirPods", StringComparison.OrdinalIgnoreCase))
            .Select(i => new PairedDeviceInfo(i.Id, i.Name))
            .ToList();
    }

    public async Task<bool> BindAsync(string deviceId)
    {
        Unbind();
        var device = await BluetoothDevice.FromIdAsync(deviceId);
        if (device is null) return false;

        _device = device;
        DeviceId = deviceId;
        DeviceName = device.Name;
        device.ConnectionStatusChanged += OnConnectionStatusChanged;
        Update(device.ConnectionStatus == BluetoothConnectionStatus.Connected, force: true);
        return true;
    }

    private void OnConnectionStatusChanged(BluetoothDevice sender, object args) =>
        Update(sender.ConnectionStatus == BluetoothConnectionStatus.Connected, force: false);

    private void Update(bool connected, bool force)
    {
        if (!force && IsConnected == connected) return;
        IsConnected = connected;
        ConnectionChanged?.Invoke(connected);
    }

    private void Unbind()
    {
        if (_device is null) return;
        _device.ConnectionStatusChanged -= OnConnectionStatusChanged;
        _device.Dispose();
        _device = null;
    }

    public void Dispose() => Unbind();
}
