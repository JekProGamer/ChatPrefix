# ChatPrefix

CounterStrikeSharp plugin for CS2 that adds colored chat prefixes.

## Features
- Default prefix for all players.
- Individual prefix overrides by Steam64 ID.
- Placeholders: `%Player%` and `%steam64%`.
- Color tokens: `{WHITE}`, `{RED}`, `{GREEN}`, `{BLUE}`, `{ORANGE}`, `{GOLD}`, `{YELLOW}`, `{PURPLE}`, `{MAGENTA}`, `{GREY}`, `{SILVER}`, `{LIGHTBLUE}`, `{DARKRED}`, `{LIME}`.
- Team chat remains team-only.
- `css_chatprefix_reload` reloads the config; permission defaults to `@css/root`.

## Configuration
Edit `config.json`:

```json
{
  "DefaultPrefix": "∆ {Orange}Rez{GOLD}Top.cc{WHITE}| %Player%",
  "ReloadPermission": "@css/root",
  "PlayerPrefixes": {
    "76561198000000000": "❤️ {RED}Coder {White}| %steam64%"
  }
}
```

Replace the example Steam64 key with the player's actual SteamID64. A custom prefix overrides the default prefix for that player.

## Install
1. Build using .NET 10:
   `dotnet restore`
   `dotnet build -c Release`
2. Copy `bin/Release/net10.0/ChatPrefix.dll` and `config.json` to:
   `game/csgo/addons/counterstrikesharp/plugins/ChatPrefix/`
3. Restart or reload the plugin.
4. To apply config edits without restarting, run `css_chatprefix_reload` as an admin.

## Notes
The plugin hooks `say` and `say_team`, prints the formatted message itself, and blocks the original chat command to avoid duplicate messages. Test with the exact CounterStrikeSharp build deployed on your server.
