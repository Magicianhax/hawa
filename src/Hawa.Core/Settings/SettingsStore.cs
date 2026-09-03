using System.Text.Json;

namespace Hawa.Core.Settings;

public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public SettingsStore(string path) => Path = path;

    public string Path { get; }

    public static string DefaultPath =>
        System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Hawa", "settings.json");

    public HawaSettings Load()
    {
        if (!File.Exists(Path))
        {
            var defaults = new HawaSettings();
            Save(defaults);
            return defaults;
        }

        try
        {
            var loaded = JsonSerializer.Deserialize<HawaSettings>(File.ReadAllText(Path), Json);
            if (loaded is not null) return loaded;
        }
        catch (JsonException) { }

        File.Copy(Path, Path + ".bad", overwrite: true);
        var fresh = new HawaSettings();
        Save(fresh);
        return fresh;
    }

    public void Save(HawaSettings settings)
    {
        var dir = System.IO.Path.GetDirectoryName(Path)!;
        Directory.CreateDirectory(dir);
        var tmp = Path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(settings, Json));
        File.Move(tmp, Path, overwrite: true);
    }
}
