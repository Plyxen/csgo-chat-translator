# CS:GO Chat Translator

A small Windows tray app for the legacy CS:GO client (Steam app 4465480) on Russian-speaking servers.

- **Russian to English:** reads the game's `console.log` and shows each Russian chat line in English in a click-through overlay on top of the game.
- **English to Russian:** press `F8`, type in English, press `Enter`. The text is translated and sent as chat. `Tab` switches between all chat and team chat.
- **Moderation view:** Russian swear words, insults and slurs are listed under the message with their literal, unsoftened meaning, from a glossary built into the app. It also catches common filter dodging (`с*ка`, `с у к а`, `п1дор`, `cyka blyat`).

Nothing is injected into the game. The app only reads the log file and talks to the game's own network console on `127.0.0.1`.

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
| Move the overlay | Tray icon, **Move overlay**, drag, **Finish moving** |
| See the Russian original | Tray icon, **Show Russian original** |

If the game is not connected, the translated text is copied to the clipboard so it can be pasted into chat.

## Translation

Free web endpoints, no key or account: two Google Translate routes and MyMemory. If one refuses a request, the next is tried. If none answers, incoming lines are shown in Russian marked "not translated" (flagged words are still explained, because the glossary is local) and outgoing messages are not sent.

Machine translation is weak on slang and tends to soften insults. Treat the flagged-word line, not the translated sentence, as the record of what was said.

## Build

Requires the .NET 10 SDK. No NuGet packages.

```powershell
dotnet publish CsgoChatTranslator -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o dist
```
