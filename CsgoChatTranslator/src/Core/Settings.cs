using System.Security.Cryptography;
using System.Text.Json;

namespace CsgoChatTranslator;

public sealed class Settings
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public string Hotkey { get; set; } = "F8";
    public int NetconPort { get; set; } = 27099;
    public string NetconPassword { get; set; } = "";
    public string TheirLanguage { get; set; } = "ru";
    public string MyLanguage { get; set; } = "en";
    public string? ConsoleLogPath { get; set; }
    public bool ShowOriginal { get; set; }
    public int MessageSeconds { get; set; } = 14;
    public int? OverlayX { get; set; }
    public int? OverlayY { get; set; }

    private string _file = "";

    public static string DefaultDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CsgoChatTranslator");

    public static Settings Load(string directory)
    {
        var file = Path.Combine(directory, "settings.json");
        Settings? settings = null;
        try
        {
            if (File.Exists(file)) settings = JsonSerializer.Deserialize<Settings>(File.ReadAllText(file));
        }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException)
        {
            // A damaged file falls back to defaults and is rewritten below.
        }

        settings ??= new Settings();
        settings._file = file;
        if (string.IsNullOrWhiteSpace(settings.NetconPassword))
        {
            settings.NetconPassword = Convert.ToHexString(RandomNumberGenerator.GetBytes(12)).ToLowerInvariant();
        }
        settings.Save();
        return settings;
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_file)!);
            File.WriteAllText(_file, JsonSerializer.Serialize(this, JsonOptions));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Settings still work for this run.
        }
    }
}
