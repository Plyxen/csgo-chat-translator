using System.Text;
using System.Text.RegularExpressions;

namespace CsgoChatTranslator;

public enum ChatTeam { None, Terrorist, CounterTerrorist, Spectator }

public sealed record ChatLine(string Name, string Text, ChatTeam Team, bool TeamOnly, bool Dead);

/// <summary>
/// Reads chat out of console.log lines. Two layouts occur on the server:
///   stock game:     [*DEAD*][(Team) ]Name&lt;U+200E&gt; : text
///   server plugin:  [*МЁРТВ*][(Команда)]  Name :  text   (two spaces after the colon)
/// </summary>
public static partial class ChatParser
{
    [GeneratedRegex(
        @"^\s*(?<state>\*[^*\s]{1,12}\*)?\s*(?:\((?<team>[^()]{1,24})\))?\s*(?<name>\S.*?)(?:\u200E(?: @ [^:]*?)?\s:\s+|(?<!\s)\s:\s{2,})(?<text>\S.*)$",
        RegexOptions.CultureInvariant)]
    private static partial Regex LinePattern();

    public static ChatLine? Parse(string line)
    {
        if (line.Length < 4 || line.Length > 800) return null;

        var match = LinePattern().Match(line);
        if (!match.Success) return null;

        var name = Clean(match.Groups["name"].Value);
        var text = Clean(match.Groups["text"].Value);
        if (name.Length == 0 || text.Length == 0) return null;

        var state = match.Groups["state"].Value.ToLowerInvariant();
        var label = match.Groups["team"].Value.ToLowerInvariant();
        var spectating = state.Contains("spec") || state.Contains("набл");
        var team = ClassifyTeam(label);
        if (team == ChatTeam.None && spectating) team = ChatTeam.Spectator;

        return new ChatLine(name, text, team, label.Length > 0, state.Length > 0 && !spectating);
    }

    public static bool HasCyrillic(string text)
    {
        var count = 0;
        foreach (var c in text)
        {
            if (c is >= '\u0400' and <= '\u04FF' && char.IsLetter(c) && ++count >= 2) return true;
        }
        return false;
    }

    public static bool HasCyrillicLetter(string text)
    {
        foreach (var c in text)
        {
            if (c is >= 'Ѐ' and <= 'ӿ') return true;
        }
        return false;
    }

    public static string Clean(string value)
    {
        var sb = new StringBuilder(value.Length);
        foreach (var c in value)
        {
            if (!char.IsControl(c) && !IsDirectionMark(c)) sb.Append(c);
        }
        return sb.ToString().Trim();
    }

    internal static bool IsDirectionMark(char c) =>
        c is '\u200E' or '\u200F' or (>= '\u202A' and <= '\u202E') or (>= '\u2066' and <= '\u2069');

    private static ChatTeam ClassifyTeam(string label)
    {
        if (label.Length == 0) return ChatTeam.None;
        if (label.Contains("counter") || label.Contains("спецназ") || label.Contains("контр")) return ChatTeam.CounterTerrorist;
        if (label.Contains("terror") || label.Contains("террор")) return ChatTeam.Terrorist;
        if (label.Contains("spect") || label.Contains("набл")) return ChatTeam.Spectator;
        return ChatTeam.None;
    }
}

public static partial class OutgoingText
{
    /// <summary>Game chat stops at about 127 bytes and Russian uses two bytes per letter.</summary>
    public const int MaxChatBytes = 120;

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    public static string Sanitize(string text)
    {
        var sb = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            if (c == '"') sb.Append('\'');
            else if (char.IsControl(c)) sb.Append(' ');
            else if (!ChatParser.IsDirectionMark(c)) sb.Append(c);
        }
        return Whitespace().Replace(sb.ToString(), " ").Trim();
    }

    public static bool IsServerCommand(string text) => text.StartsWith('!') || text.StartsWith('/');

    public static string ToConsoleCommand(string text, bool teamOnly) =>
        $"{(teamOnly ? "say_team" : "say")} \"{Sanitize(text)}\"";

    public static List<string> Split(string text, int maxBytes = MaxChatBytes)
    {
        var parts = new List<string>();
        var current = new StringBuilder();
        var currentBytes = 0;

        void Flush()
        {
            if (current.Length == 0) return;
            parts.Add(current.ToString());
            current.Clear();
            currentBytes = 0;
        }

        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var wordBytes = Encoding.UTF8.GetByteCount(word);
            if (wordBytes > maxBytes)
            {
                Flush();
                foreach (var rune in word.EnumerateRunes())
                {
                    if (currentBytes + rune.Utf8SequenceLength > maxBytes) Flush();
                    current.Append(rune.ToString());
                    currentBytes += rune.Utf8SequenceLength;
                }
                continue;
            }

            var separator = current.Length > 0 ? 1 : 0;
            if (currentBytes + separator + wordBytes > maxBytes)
            {
                Flush();
                separator = 0;
            }
            if (separator == 1) current.Append(' ');
            current.Append(word);
            currentBytes += separator + wordBytes;
        }

        Flush();
        return parts;
    }
}
