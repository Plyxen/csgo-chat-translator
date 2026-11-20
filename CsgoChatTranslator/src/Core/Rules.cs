namespace CsgoChatTranslator;

/// <summary>
/// The server's rules that a chat message can break, given to Claude so it can name the rule and the penalty
/// next to the translation. The text lives in rules.txt beside the settings, so it can be edited without rebuilding.
/// </summary>
public static class Rules
{
    public const string FileName = "rules.txt";

    public const string Default = """
        Chat rules (text and voice). A violation in reply to another player is still a violation.
        Chat 1: insults based on gender, race, nationality, language, origin, religion or membership of a social group. Penalty: warning, then microphone/chat block for up to 30 days.
        Chat 1.1: insulting anyone's relatives or loved ones (for example insults about someone's mother). Penalty: microphone and chat block for 30 days, no warning.
        Chat 1.2: mentioning anyone's relatives or loved ones in a negative way. Penalty: microphone and chat block for 24 hours, no warning.
        Chat 1.4: starting or continuing political arguments or discussions. Penalty: warning, then microphone/chat block for 60 minutes.

        General rules.
        General 1.2: pretending to be server or project staff. Penalty: warning, then 1 day ban. As an obvious joke: warning, then microphone/chat block for 60 minutes.
        General 1.3: advertising other servers, projects or resources that compete with or harm this one. Twitch and YouTube channels and roulette sites do not count. Penalty: permanent ban. Advertising cheats: permanent ban.
        General 1.4: destructive behaviour towards the project: non-constructive criticism of it, calling on players to leave it. Penalty: ban from 14 days to permanent.
        General 1.5: ghosting, meaning a dead or spectating player telling living players where enemies are. Penalty: warning, then microphone/chat block for 24 hours.
        General 1.9: insulting, provoking or trolling players. Penalty: warning, then microphone/chat block for up to 30 days. Insults sent to the admin chat (messages starting with @): block from 60 minutes to 24 hours.
        General 1.10: excessive swearing. Swearing in reasonable amounts is allowed. Penalty: warning, then microphone/chat block for 1 hour.
        General 1.11: spamming or flooding the chat. Penalty: warning, then microphone/chat block from 1 hour. Spam in the admin chat (@): block from 10 to 60 minutes.
        General 1.14: any kind of scam. Penalty: permanent ban.
        General 1.16: inviting players to matchmaking, one-on-one games and the like outside this project. Penalty: warning, then ban from 6 hours.
        """;

    /// <summary>Reads rules.txt from the settings folder, creating it with the built-in rules the first time.</summary>
    public static string Load(string directory)
    {
        var file = Path.Combine(directory, FileName);
        try
        {
            if (File.Exists(file))
            {
                var text = File.ReadAllText(file).Trim();
                if (text.Length > 0) return text;
            }
            else
            {
                Directory.CreateDirectory(directory);
                File.WriteAllText(file, Default);
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // The built-in rules still work for this run.
        }
        return Default;
    }
}
