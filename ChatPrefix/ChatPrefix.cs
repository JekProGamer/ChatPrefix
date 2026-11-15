using CounterStrikeSharp.API.Modules.Admin;
using System.Text.Json;
using System.Text.RegularExpressions;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;

namespace ChatPrefix;

public sealed class ChatPrefix : BasePlugin
{
    public override string ModuleName => "ChatPrefix";
    public override string ModuleVersion => "1.0.1";
    public override string ModuleAuthor => "JekProGamer";
    public override string ModuleDescription => "Per-player configurable colored chat prefixes.";

    private PluginConfig _config = new();
    private string ConfigPath => Path.Combine(ModuleDirectory, "config.json");

    private static readonly Regex ColorToken = new(
        @"\{(?<color>[A-Za-z]+)\}",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public override void Load(bool hotReload)
    {
        LoadConfig();
        AddCommandListener("say", OnSay);
        AddCommandListener("say_team", OnSayTeam);
        AddCommand("css_chatprefix_reload", "Reload ChatPrefix configuration", OnReloadCommand);
        Logger.LogInformation("ChatPrefix loaded. Config: {ConfigPath}", ConfigPath);
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

    private HookResult HandleChat(CCSPlayerController? player, CommandInfo command, bool teamOnly)
    {
        if (player == null || !player.IsValid || command.ArgCount < 2)
            return HookResult.Handled;

        var message = command.ArgString.Trim();
        if (string.IsNullOrWhiteSpace(message))
            return HookResult.Handled;

        if (message.Length >= 2 && message[0] == '"' && message[^1] == '"')
            message = message[1..^1];

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
    }
}
