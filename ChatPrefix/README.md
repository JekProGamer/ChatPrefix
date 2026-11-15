# ChatPrefix

CounterStrikeSharp plugin for CS2 with colored chat prefixes and anti-flood protection.

## Features
- Default prefix for all players.
- Individual prefix overrides by Steam64 ID.
- Placeholder: `%player%` — replaced with the sender's current player name.
- Color tokens: `{WHITE}`, `{RED}`, `{GREEN}`, `{BLUE}`, `{ORANGE}`, `{GOLD}`, `{YELLOW}`, `{PURPLE}`, `{MAGENTA}`, `{GREY}`, `{SILVER}`, `{LIGHTBLUE}`, `{DARKRED}`, `{LIME}`.
- Team chat remains team-only.
- Configurable anti-flood for public and team chat.
- Ignore anti-flood by Steam64 ID, admin permission flags, or admin groups.
- `css_chatprefix_reload` reloads the config; permission defaults to `@css/root`.

## Configuration

```json
{
  "DefaultPrefix": "∆ {Orange}Rez{GOLD}Top.cc{WHITE}| %player%",
  "ReloadPermission": "@css/root",
  "PlayerPrefixes": {
    "76561198000000000": "❤️ {RED}Coder {White}| %player%"
  },
  "AntiFlood_Messages": 0,
  "AntiFlood_Seconds": 5,
  "AntiFlood_PunishCooldown": 10,
  "AntiFlood_Ignore_Flags": "SteamIDs: | Flags: | Groups: "
}
```

### Anti-flood settings
- `AntiFlood_Messages`: number of messages in the time window that triggers a block. Set to `0` to disable anti-flood.
- `AntiFlood_Seconds`: rolling time window, in seconds.
- `AntiFlood_PunishCooldown`: how long, in seconds, the player is blocked from chat after reaching the message limit.
- `AntiFlood_Ignore_Flags`: comma-separated exclusions grouped into sections separated by `|`:
  - `SteamIDs: 76561198206086993`
  - `Flags: @css/root,@css/admin`
  - `Groups: #css/root,#css/admin`
  - Use `SteamIDs: | Flags: | Groups: ` to exclude nobody (anti-flood applies to everyone).

Example allowing admins and one Steam account to bypass anti-flood:
`SteamIDs: 76561198206086993 | Flags: @css/root,@css/admin | Groups: #css/root,#css/admin`

The value `steam64` by itself is not a valid exclusion rule. Put an actual Steam64 ID after `SteamIDs:`.

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
