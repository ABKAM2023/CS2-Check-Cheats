using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Reflection;
using CSTimers = CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Menu;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Entities;
using CounterStrikeSharp.API.Modules.Utils;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace CheckCheatsPlugin
{
    public class CheckCheatsPlugin : BasePlugin
    {
        public override string ModuleName => "[CheckCheats] by ABKAM";
        public override string ModuleVersion => "1.0.1";
        private Dictionary<CCSPlayerController, CSTimers.Timer> checkTimers = new Dictionary<CCSPlayerController, CSTimers.Timer>();
        private Dictionary<CCSPlayerController, (string message, bool continueUpdating)> playerCenterMessages = new Dictionary<CCSPlayerController, (string message, bool continueUpdating)>();
        private Dictionary<CCSPlayerController, bool> isCheckActive = new Dictionary<CCSPlayerController, bool>();
        private Dictionary<CCSPlayerController, CSTimers.Timer> playerMessageTimers = new Dictionary<CCSPlayerController, CSTimers.Timer>();
        private Dictionary<CCSPlayerController, CCSPlayerController> adminInitiatingCheck = new Dictionary<CCSPlayerController, CCSPlayerController>(); 
        private PluginConfig _config;

        private void LoadConfig()
        {
            string configFilePath = Path.Combine(ModuleDirectory, "Config.yml");
            if (!File.Exists(configFilePath))
            {
                _config = new PluginConfig();
                SaveConfig(_config, configFilePath); 
            }
            else
            {
                string yamlConfig = File.ReadAllText(configFilePath);
                var deserializer = new DeserializerBuilder().Build();
                _config = deserializer.Deserialize<PluginConfig>(yamlConfig) ?? new PluginConfig();
            }
        }

        private void SaveConfig(PluginConfig config, string filePath)
        {
            var stringBuilder = new StringBuilder();

            stringBuilder.AppendLine("# Configuration file for the CheckCheatsPlugin");

            stringBuilder.AppendLine("# Command format for banning players (mm_ban {0} time {1})");
            AppendConfigValue(stringBuilder, nameof(config.BanCommand), config.BanCommand);

            stringBuilder.AppendLine("# Reason for player ban");
            AppendConfigValue(stringBuilder, nameof(config.BanReason), config.BanReason);

            stringBuilder.AppendLine("# Duration of player check in seconds");
            AppendConfigValue(stringBuilder, nameof(config.CheckDuration), config.CheckDuration);

            stringBuilder.AppendLine("# Format of countdown message during player check");
            AppendConfigValue(stringBuilder, nameof(config.CountdownMessageFormat), config.CountdownMessageFormat);

            stringBuilder.AppendLine("# Error message when contact information is not provided correctly");
            AppendConfigValue(stringBuilder, nameof(config.ErrorMessage), config.ErrorMessage);

            stringBuilder.AppendLine("# Message format for administrator after player provides contact information");
            AppendConfigValue(stringBuilder, nameof(config.AdminMessageFormat), config.AdminMessageFormat);

            stringBuilder.AppendLine("# Message displayed to the player after successfully passing the check");
            AppendConfigValue(stringBuilder, nameof(config.SuccessMessage), config.SuccessMessage);

            File.WriteAllText(filePath, stringBuilder.ToString());
        }    
        private void AppendConfigValue(StringBuilder stringBuilder, string key, object value)
        {
            var valueStr = value?.ToString() ?? string.Empty;
            stringBuilder.AppendLine($"{key}: \"{EscapeMessage(valueStr)}\"");
        }

        private string EscapeMessage(string message)
        {
            return message.Replace("\"", "\\\"");
        }           
        public override void Load(bool hotReload)
        {
            AddCommand("check", "Check a player for cheats", AdminCheckCommand);
            AddCommand("contact", "Send contact information", PlayerContactCommand);
            AddCommand("uncheck", "Stop checking a player", AdminUncheckCommand);
            RegisterListener<Listeners.OnTick>(() =>
            {
                foreach (var kvp in playerCenterMessages)
                {
                    var player = kvp.Key;
                    var (message, _) = kvp.Value;
                    if (player != null && player.IsValid)
                    {
                        player.PrintToCenterHtml(message);
                    }
                }
            });
            LoadConfig();
        }     
        [RequiresPermissions("@admin/uncheck")]        
        private void AdminUncheckCommand(CCSPlayerController? caller, CommandInfo info)
        {
            if (caller == null) return;

            var playerSelectMenu = new ChatMenu("Select a player to stop checking");
            foreach (var player in Utilities.GetPlayers())
            {
                string playerName = player.PlayerName;
                playerSelectMenu.AddMenuOption(playerName, (admin, option) => UncheckPlayer(player));
            }
            ChatMenus.OpenMenu(caller, playerSelectMenu);
        }
        private void UncheckPlayer(CCSPlayerController playerToUncheck)
        {
            if (playerMessageTimers.ContainsKey(playerToUncheck))
            {
                var timer = playerMessageTimers[playerToUncheck];
                timer.Kill();
                playerMessageTimers.Remove(playerToUncheck);
            }

            isCheckActive[playerToUncheck] = false; 

            string successMessage = _config.SuccessMessage;
            playerCenterMessages[playerToUncheck] = (successMessage, true);

            var messageRemovalTimer = AddTimer(3, () =>
            {
                playerCenterMessages.Remove(playerToUncheck);
            });

            playerMessageTimers[playerToUncheck] = messageRemovalTimer;
        }


        [RequiresPermissions("@admin/check")]
        private void AdminCheckCommand(CCSPlayerController? caller, CommandInfo info)
        {
            if (caller == null) return;

            var playerSelectMenu = new ChatMenu("Select a player to check");
            foreach (var player in Utilities.GetPlayers())
            {
                string playerName = player.PlayerName;
                playerSelectMenu.AddMenuOption(playerName, (admin, option) => CheckPlayer(player, admin));
            }
            ChatMenus.OpenMenu(caller, playerSelectMenu);
        }
        private void CheckPlayer(CCSPlayerController playerToCheck, CCSPlayerController admin)
        {
            playerToCheck.ChangeTeam(CsTeam.Spectator);

            adminInitiatingCheck[playerToCheck] = admin;
            int totalTime = _config.CheckDuration;
            ShowCenterMessageWithCountdown(playerToCheck, totalTime);

            var timer = AddTimer(totalTime, () =>
            {
                BanPlayer(playerToCheck);
                playerCenterMessages.Remove(playerToCheck);
                playerMessageTimers.Remove(playerToCheck);
            });

            playerMessageTimers[playerToCheck] = timer;
        }


        private void PlayerContactCommand(CCSPlayerController player, CommandInfo info)
        {
            if (info.ArgCount < 2)
            {
                string errorMessage = ReplaceColorPlaceholders(_config.ErrorMessage);
                player.PrintToChat(errorMessage);
                return;
            }

            string discordContact = info.GetArg(1);

            if (adminInitiatingCheck.TryGetValue(player, out var admin))
            {
                string message = _config.AdminMessageFormat
                                .Replace("{PlayerName}", player.PlayerName)
                                .Replace("{DiscordContact}", discordContact);
                message = ReplaceColorPlaceholders(message);
                admin.PrintToChat(message);
            }
            if (playerMessageTimers.TryGetValue(player, out var timer))
            {
                timer.Kill();
                playerMessageTimers.Remove(player);
                isCheckActive[player] = false; 
            }

            if (playerCenterMessages.TryGetValue(player, out var messageInfo))
            {
                playerCenterMessages[player] = (messageInfo.message, false); 
            }

        }
        private void BanPlayer(CCSPlayerController player)
        {
            string banCommand = string.Format(_config.BanCommand, player.SteamID, _config.BanReason);
            Server.ExecuteCommand(banCommand);
        }
        private void ShowCenterMessageWithCountdown(CCSPlayerController player, int totalTime)
        {
            if (playerMessageTimers.TryGetValue(player, out var existingTimer))
            {
                existingTimer.Kill();
                playerMessageTimers.Remove(player);
            }

            int remainingTime = totalTime;
            isCheckActive[player] = true;


            CSTimers.Timer messageTimer = null;
            messageTimer = AddTimer(1, () =>
            {
                if (!isCheckActive[player] || messageTimer == null)
                {
                    if (messageTimer != null)
                    {
                        messageTimer.Kill();
                        playerMessageTimers.Remove(player);
                    }
                    playerCenterMessages[player] = (playerCenterMessages[player].message, false);
                    return;
                }

                if (remainingTime <= 0)
                {
                    playerCenterMessages[player] = (playerCenterMessages[player].message, false);
                    isCheckActive[player] = false;
                    if (messageTimer != null)
                    {
                        messageTimer.Kill();
                        playerMessageTimers.Remove(player);
                    }
                    return;
                }

                remainingTime--;
                string message = _config.CountdownMessageFormat
                                .Replace("{remainingTime}", remainingTime.ToString());

                playerCenterMessages[player] = (message, true);
            }, CSTimers.TimerFlags.REPEAT);

            playerMessageTimers[player] = messageTimer;
        }
        private string ReplaceColorPlaceholders(string message)
        {
            if (message.Contains('{'))
            {
                string modifiedValue = message;
                foreach (FieldInfo field in typeof(ChatColors).GetFields())
                {
                    string pattern = $"{{{field.Name}}}";
                    if (message.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                    {
                        modifiedValue = modifiedValue.Replace(pattern, field.GetValue(null).ToString(), StringComparison.OrdinalIgnoreCase);
                    }
                }
                return modifiedValue;
            }

            return message;
        }          
        public class PluginConfig
        {
            public string BanCommand { get; set; } = "mm_ban {0} 0 {1}";  
            public string BanReason { get; set; } = "Cheating"; 
            public int CheckDuration { get; set; } = 120; 
            public string CountdownMessageFormat { get; set; } = "<font color='red' class='fontSize-l'>You have been called for a check. Please provide your Discord. Remaining time: {remainingTime} sec. Type !contact your_discord.</font>";
            public string ErrorMessage { get; set; } = "[{Red}ADMIN{White}] Please provide your Discord. Use: {Green}!contact your_discord";
            public string AdminMessageFormat { get; set; } = "[{Red}ADMIN{White}] Player {Yellow}{PlayerName} {White}has provided their Discord: {Green}{DiscordContact}";
            public string SuccessMessage { get; set; } = "<font color='green' class='fontSize-l'>You have successfully passed the check.</font>";
        }
    }
}
