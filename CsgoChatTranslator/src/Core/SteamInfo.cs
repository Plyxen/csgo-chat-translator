using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace CsgoChatTranslator;

/// <summary>Finds the legacy CS:GO install and reads its Steam launch options. Never writes to Steam.</summary>
public static partial class SteamInfo
{
    private const string AppId = "4465480";

    [GeneratedRegex("\"path\"\\s+\"((?:[^\"\\\\]|\\\\.)*)\"")]
    private static partial Regex LibraryPath();

    [GeneratedRegex("\"installdir\"\\s+\"((?:[^\"\\\\]|\\\\.)*)\"")]
    private static partial Regex InstallDir();

    [GeneratedRegex("\"" + AppId + "\"\\s*\\{")]
    private static partial Regex AppBlock();

    [GeneratedRegex("\"LaunchOptions\"\\s+\"((?:[^\"\\\\]|\\\\.)*)\"", RegexOptions.IgnoreCase)]
    private static partial Regex LaunchOptionsValue();

    [GeneratedRegex(@"(^|\s)-netcon(port|password)(\s+[^\s-]\S*)?")]
    private static partial Regex NetconOption();

    [GeneratedRegex(@"(^|\s)-condebug(\s|$)")]
    private static partial Regex CondebugOption();

    public static string? SteamRoot()
    {
        var value = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", null) as string;
        return string.IsNullOrWhiteSpace(value) ? null : Path.GetFullPath(value.Replace('/', '\\'));
    }

    public static string? FindConsoleLog()
    {
        var root = SteamRoot();
        if (root is null) return null;

        var libraries = new List<string> { root };
        try
        {
            var vdf = File.ReadAllText(Path.Combine(root, "steamapps", "libraryfolders.vdf"));
            libraries.AddRange(LibraryPath().Matches(vdf).Select(m => Unescape(m.Groups[1].Value)));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }

        foreach (var library in libraries.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                var manifest = Path.Combine(library, "steamapps", $"appmanifest_{AppId}.acf");
                if (!File.Exists(manifest)) continue;
                var match = InstallDir().Match(File.ReadAllText(manifest));
                if (match.Success)
                {
                    return Path.Combine(library, "steamapps", "common", Unescape(match.Groups[1].Value), "csgo", "console.log");
                }
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        }
        return null;
    }

    public static string ReadLaunchOptions()
    {
        var root = SteamRoot();
        var userdata = root is null ? null : Path.Combine(root, "userdata");
        if (userdata is null || !Directory.Exists(userdata)) return "";

        var files = Directory.GetDirectories(userdata)
            .Select(d => new FileInfo(Path.Combine(d, "config", "localconfig.vdf")))
            .Where(f => f.Exists)
            .OrderByDescending(f => f.LastWriteTimeUtc);

        foreach (var file in files)
        {
            try
            {
                var options = ExtractLaunchOptions(File.ReadAllText(file.FullName));
                if (!string.IsNullOrWhiteSpace(options)) return options;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        }
        return "";
    }

    public static string? ExtractLaunchOptions(string vdf)
    {
        foreach (Match block in AppBlock().Matches(vdf))
        {
            var start = block.Index + block.Length;
            var end = FindBlockEnd(vdf, start);
            var match = LaunchOptionsValue().Match(vdf, start, end - start);
            if (match.Success) return Unescape(match.Groups[1].Value);
        }
        return null;
    }

    public static string BuildLaunchOptions(string existing, int port, string password)
    {
        var options = NetconOption().Replace(existing, " ").Trim();
        if (!CondebugOption().IsMatch(options)) options = $"{options} -condebug".Trim();
        return $"{options} -netconport {port} -netconpassword {password}";
    }

    public static bool HasLaunchOptions(string existing, int port, string password) =>
        CondebugOption().IsMatch(existing) &&
        Regex.IsMatch(existing, $@"(^|\s)-netconport\s+{port}(\s|$)") &&
        Regex.IsMatch(existing, $@"(^|\s)-netconpassword\s+{Regex.Escape(password)}(\s|$)");

    private static int FindBlockEnd(string text, int start)
    {
        var depth = 1;
        var inString = false;
        for (var i = start; i < text.Length; i++)
        {
            var c = text[i];
            if (inString)
            {
                if (c == '\\') i++;
                else if (c == '"') inString = false;
            }
            else if (c == '"') inString = true;
            else if (c == '{') depth++;
            else if (c == '}' && --depth == 0) return i;
        }
        return text.Length;
    }

    private static string Unescape(string value) => value.Replace("\\\\", "\\").Replace("\\\"", "\"");
}
