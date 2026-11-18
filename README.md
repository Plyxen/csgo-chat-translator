# CS:GO Chat Translator

A small Windows tray app for the legacy CS:GO client (Steam app 4465480) on Russian-speaking servers.

- **Russian to English:** reads the game's `console.log` and shows each Russian chat line in English in a click-through overlay on top of the game.
- **English to Russian:** press `F8`, type in English, press `Enter`. The text is translated and sent as chat. `Tab` switches between all chat and team chat.
- **Moderation view:** Russian swear words, insults and slurs are listed under the message with their literal, unsoftened meaning, from a glossary built into the app. It also catches common filter dodging (`с*ка`, `с у к а`, `п1дор`, `cyka blyat`).

Nothing is injected into the game. The app only reads the log file and talks to the game's own network console on `127.0.0.1`.

It works out of the box for free, with no account or key. Adding a Claude Haiku key makes the translations noticeably better, but it is an optional extra, not a requirement.

## Setup

1. Run `CsgoChatTranslator.exe`. It sits in the system tray.
2. Open **Status and setup** from the tray icon and click **Copy launch options**.
3. In Steam, open the game's Properties and paste over **Launch Options**. Your existing options are kept; the app adds `-condebug -netconport <port> -netconpassword <random>`.
4. Start the game. Play in windowed or borderless mode; the overlay cannot draw over exclusive fullscreen.

Settings live in `%APPDATA%\CsgoChatTranslator\settings.json` (hotkey, port, overlay position, languages).

## Using it

| Action | How |
| --- | --- |
| Write a message | `F8`, type, `Enter` |
| Team chat | `Tab` in the message box |
| Server commands | Anything starting with `!` or `/` is sent as typed |
| Pause or resume translating | `Shift+F8`, the tray menu, or the button in **Status and setup** |
| Move the overlay | Tray icon, **Move overlay**, drag, **Finish moving** |
| See the Russian original | Tray icon, **Show Russian original** |

If the game is not connected, the translated text is copied to the clipboard so it can be pasted into chat.

Pause when you are on the server but not playing. While paused, incoming chat is not read or sent to any translator, so nothing is billed if you use a Claude key, and lines that arrive during the pause are dropped rather than translated later. Writing a message with `F8` still works.

## Translation

### Free routes: the default, nothing to set up

Web endpoints that need no key or account: two Google Translate routes and MyMemory. If one refuses a request, the next is tried. If none answers, incoming lines are shown in Russian marked "not translated" (flagged words are still explained, because the glossary is local) and outgoing messages are not sent.

This is enough to follow the chat and to be understood. Its weak spots are slang and tone: free machine translation tends to garble shorthand and soften insults, which is why the flagged-word line exists.

### Claude Haiku: optional, good to have

You do not need this. The app is fully usable without it, and if a key is missing, rejected, out of credit, or the API is unreachable, the app quietly uses the free routes.

If you want better translations, paste an Anthropic API key under **Better translations** in the status window. Chat is then translated with `claude-haiku-5-5` first, and the free routes become the fallback. What you gain:

- Slang, typos and shorthand are understood (`хз че он творит, изи катка` becomes "idk what he's doing, easy game").
- Insults stay as rude as they were written.
- Russian typed in Latin letters (`privet kak dela`) is translated, not only flagged.
- Your own messages come out in natural player Russian.

It uses the pay-as-you-go Anthropic API, not a Claude subscription. A chat line costs roughly $0.00004 and takes under a second, so a few dollars of prepaid credit lasts months. The key is stored encrypted for your Windows user in the settings file, outside this repository (or read from `ANTHROPIC_API_KEY`).

Either way, treat the flagged-word line, not the translated sentence, as the record of what was said.

## Build

Requires the .NET 10 SDK. One NuGet package: the official `Anthropic` SDK, pinned.

```powershell
dotnet publish CsgoChatTranslator -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o dist
```
