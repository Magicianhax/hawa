using Hawa.Core.Settings;

namespace Hawa.Tests.Settings;

public class SettingsStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "HawaTests", Guid.NewGuid().ToString("N"));
    private string FilePath => Path.Combine(_dir, "settings.json");

    public void Dispose() { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); }

    [Fact]
    public void Missing_file_returns_defaults_and_creates_file()
    {
        var store = new SettingsStore(FilePath);
        var s = store.Load();
        Assert.Equal(new HawaSettings(), s);
        Assert.True(File.Exists(FilePath));
    }

    [Fact]
    public void Round_trips_all_fields()
    {
        var store = new SettingsStore(FilePath);
        var s = new HawaSettings
        {
            SelectedDeviceId = "Bluetooth#Bluetoothaa:bb-cc:dd",
            AutoPauseEnabled = false,
            PauseOnlyWhenBothRemoved = true,
            PopupOnConnect = false,
            PopupOnLidOpen = false,
            PopupDurationSeconds = 9,
            StartWithWindows = true,
            LowBatteryThreshold = 15,
            LastNoiseMode = 3,
        };
        store.Save(s);
        Assert.Equal(s, new SettingsStore(FilePath).Load());
    }

    [Fact]
    public void Corrupt_file_is_backed_up_and_replaced_with_defaults()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(FilePath, "{ not json");
        var s = new SettingsStore(FilePath).Load();
        Assert.Equal(new HawaSettings(), s);
        Assert.True(File.Exists(FilePath + ".bad"));
        Assert.Equal(new HawaSettings(), new SettingsStore(FilePath).Load());
    }

    [Fact]
    public void Save_leaves_no_temp_file_behind()
    {
        var store = new SettingsStore(FilePath);
        store.Save(new HawaSettings());
        Assert.Empty(Directory.GetFiles(_dir, "*.tmp"));
    }
}
