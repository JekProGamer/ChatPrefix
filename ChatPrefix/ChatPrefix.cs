using CounterStrikeSharp.API.Modules.Admin;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.RegularExpressions;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Events;
using CounterStrikeSharp.API.Modules.Utils;

namespace ChatPrefix;

public sealed class ChatPrefix : BasePlugin
{
    public override string ModuleName => "ChatPrefix";
    public override string ModuleVersion => "1.1.3";
    public override string ModuleAuthor => "JekProGamer";
    public override string ModuleDescription => "Colored chat prefixes with configurable anti-flood protection.";

    private PluginConfig _config = new();
    private string ConfigPath => Path.Combine(ModuleDirectory, "config.json");

    private readonly Dictionary<ulong, Queue<DateTime>> _messageHistory = new();
    private readonly Dictionary<ulong, DateTime> _punishedUntil = new();

    private static readonly Regex ColorToken = new(
        @"\{(?<color>[A-Za-z]+)\}",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public override void Load(bool hotReload)
    {
        LoadConfig();
        AddCommandListener("say", OnSay, HookMode.Pre);
        AddCommandListener("say_team", OnSayTeam, HookMode.Pre);
        RegisterEventHandler<EventPlayerChat>(OnPlayerChat, HookMode.Pre);
        AddCommand("css_chatprefix_reload", "Reload ChatPrefix configuration", OnReloadCommand);
        Logger.LogInformation("ChatPrefix v{Version} loaded. Config: {ConfigPath}", ModuleVersion, ConfigPath);
    }

    public override void Unload(bool hotReload)
    {
        RemoveCommandListener("say", OnSay, HookMode.Pre);
        RemoveCommandListener("say_team", OnSayTeam, HookMode.Pre);
        _messageHistory.Clear();
        _punishedUntil.Clear();
    }

    private void LoadConfig()
    {
        if (!File.Exists(ConfigPath))
        {
            Directory.CreateDirectory(ModuleDirectory);
            File.WriteAllText(ConfigPath, JsonSerializer.Serialize(new PluginConfig(),
                new JsonSerializerOptions { WriteIndented = true }));
        }

        try
        {
            _config = JsonSerializer.Deserialize<PluginConfig>(File.ReadAllText(ConfigPath),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new PluginConfig();

            _config.PlayerPrefixes ??= new Dictionary<string, string>();
            _config.AntiFlood_Ignore_Flags ??= "SteamIDs: | Flags: | Groups: ";
            _config.AntiFlood_Messages = Math.Max(0, _config.AntiFlood_Messages);
            _config.AntiFlood_Seconds = Math.Max(1, _config.AntiFlood_Seconds);
            _config.AntiFlood_PunishCooldown = Math.Max(1, _config.AntiFlood_PunishCooldown);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Could not read ChatPrefix config. Keeping current/default config.");
        }
    }

    private HookResult OnSay(CCSPlayerController? player, CommandInfo command)
        => HandleChat(player, command, teamOnly: false);

    private HookResult OnSayTeam(CCSPlayerController? player, CommandInfo command)
        => HandleChat(player, command, teamOnly: true);

    // Chat commands are executed by CounterStrikeSharp during chat processing.
    // Suppress only their broadcast event, without blocking command dispatch.
    private HookResult OnPlayerChat(EventPlayerChat @event, GameEventInfo info)
    {
        var message = @event.Text?.TrimStart();
        if (!string.IsNullOrEmpty(message) && message.StartsWith("!", StringComparison.Ordinal))
        {
            info.DontBroadcast = true;
            return HookResult.Handled;
        }

        return HookResult.Continue;
    }

    private HookResult HandleChat(CCSPlayerController? player, CommandInfo command, bool teamOnly)
    {
        if (player == null || !player.IsValid || command.ArgCount < 2)
            return HookResult.Handled;

        var message = command.ArgString.Trim();
        if (string.IsNullOrWhiteSpace(message))
            return HookResult.Handled;

        if (message.Length >= 2 && message[0] == '"' && message[^1] == '"')
            message = message[1..^1];

        // Never intercept chat commands. Let AdminPlus and every other plugin
        // process any command beginning with ! (and slash-style commands too).
        // Bypass both prefix formatting and anti-flood for command messages.
        var commandText = message.TrimStart();
        if (commandText.StartsWith("!", StringComparison.Ordinal) ||
            commandText.StartsWith("/", StringComparison.Ordinal))
            return HookResult.Continue;

        if (IsAntiFloodPunished(player))
            return HookResult.Handled;

        var prefixTemplate = _config.PlayerPrefixes.TryGetValue(player.SteamID.ToString(), out var customPrefix)
            ? customPrefix
            : _config.DefaultPrefix;

        var prefix = FormatTemplate(prefixTemplate, player);
        var output = $"{prefix} {ChatColors.White}{message}";

        foreach (var recipient in Utilities.GetPlayers())
        {
            if (!recipient.IsValid)
                continue;

            if (teamOnly && recipient.Team != player.Team)
                continue;

            recipient.PrintToChat(output);
        }

        return HookResult.Handled;
    }

    private bool IsAntiFloodPunished(CCSPlayerController player)
    {
        if (_config.AntiFlood_Messages <= 0 || IsAntiFloodIgnored(player))
            return false;

        var steamId = player.SteamID;
        var now = DateTime.UtcNow;

        if (_punishedUntil.TryGetValue(steamId, out var blockedUntil))
        {
            if (now < blockedUntil)
            {
                // The punishment notice is sent once, when the mute starts.
                // Further chat attempts are silently blocked to avoid flooding chat with warnings.
                return true;
            }

            _punishedUntil.Remove(steamId);
        }

        if (!_messageHistory.TryGetValue(steamId, out var history))
        {
            history = new Queue<DateTime>();
            _messageHistory[steamId] = history;
        }

        var windowStart = now.AddSeconds(-_config.AntiFlood_Seconds);
        while (history.Count > 0 && history.Peek() < windowStart)
            history.Dequeue();

        history.Enqueue(now);

        if (history.Count < _config.AntiFlood_Messages)
            return false;

        _messageHistory.Remove(steamId);
        var cooldown = TimeSpan.FromSeconds(_config.AntiFlood_PunishCooldown);
        _punishedUntil[steamId] = now.Add(cooldown);

        player.PrintToChat($" {ChatColors.Red}AntiFood {ChatColors.White}| {ChatColors.Silver}Спам блокировка чата на {ChatColors.Green}{_config.AntiFlood_PunishCooldown} {ChatColors.Silver}Секунд.");
        return true;
    }

    private bool IsAntiFloodIgnored(CCSPlayerController player)
    {
        var setting = _config.AntiFlood_Ignore_Flags;
        if (string.IsNullOrWhiteSpace(setting))
            return false;

        var steamIds = new List<string>();
        var flags = new List<string>();
        var groups = new List<string>();

        foreach (var section in setting.Split('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = section.IndexOf(':');
            if (separator < 0)
                continue;

            var key = section[..separator].Trim();
            var values = section[(separator + 1)..]
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

            if (key.Equals("SteamIDs", StringComparison.OrdinalIgnoreCase))
                steamIds.AddRange(values);
            else if (key.Equals("Flags", StringComparison.OrdinalIgnoreCase))
                flags.AddRange(values);
            else if (key.Equals("Groups", StringComparison.OrdinalIgnoreCase))
                groups.AddRange(values);
        }

        if (steamIds.Contains(player.SteamID.ToString(), StringComparer.OrdinalIgnoreCase))
            return true;

        if (flags.Any(flag => AdminManager.PlayerHasPermissions(player, flag)))
            return true;

        if (groups.Any(group => AdminManager.PlayerInGroup(player, group)))
            return true;

        return false;
    }

    private string FormatTemplate(string template, CCSPlayerController player)
    {
        var output = template.Replace("%player%", player.PlayerName, StringComparison.OrdinalIgnoreCase);

        return ColorToken.Replace(output, match =>
        {
            var color = match.Groups["color"].Value.ToUpperInvariant();
            return color switch
            {
                "WHITE" => ChatColors.White.ToString(),
                "RED" => ChatColors.Red.ToString(),
                "DARKRED" => ChatColors.DarkRed.ToString(),
                "GREEN" => ChatColors.Green.ToString(),
                "LIME" => ChatColors.Lime.ToString(),
                "BLUE" => ChatColors.Blue.ToString(),
                "LIGHTBLUE" => ChatColors.LightBlue.ToString(),
                "PURPLE" => ChatColors.Purple.ToString(),
                "MAGENTA" => ChatColors.Magenta.ToString(),
                "YELLOW" => ChatColors.Yellow.ToString(),
                "GOLD" => ChatColors.Gold.ToString(),
                "ORANGE" => ChatColors.Orange.ToString(),
                "GREY" or "GRAY" => ChatColors.Grey.ToString(),
                "SILVER" => ChatColors.Silver.ToString(),
                _ => match.Value
            };
        });
    }

    private void OnReloadCommand(CCSPlayerController? player, CommandInfo command)
    {
        if (player != null && !AdminManager.PlayerHasPermissions(player, _config.ReloadPermission))
        {
            player.PrintToChat($"{ChatColors.Red}[ChatPrefix] Нет доступа.");
            return;
        }

        LoadConfig();
        _messageHistory.Clear();
        _punishedUntil.Clear();

        if (player != null && player.IsValid)
            player.PrintToChat($"{ChatColors.Green}[ChatPrefix] Конфигурация перезагружена.");
        else
            Logger.LogInformation("ChatPrefix configuration reloaded.");
    }

    private sealed class PluginConfig
    {
        public string DefaultPrefix { get; set; } = "∆ {Orange}Rez{GOLD}Top.cc{WHITE}| %player%";
        public string ReloadPermission { get; set; } = "@css/root";
        public Dictionary<string, string> PlayerPrefixes { get; set; } = new()
        {
            ["76561198000000000"] = "❤️ {RED}Coder {WHITE}| %player%"
        };

        public int AntiFlood_Messages { get; set; } = 0;
        public int AntiFlood_Seconds { get; set; } = 5;
        public int AntiFlood_PunishCooldown { get; set; } = 10;
        public string AntiFlood_Ignore_Flags { get; set; } = "SteamIDs: | Flags: | Groups: ";
    }
}
