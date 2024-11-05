﻿using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Core.Capabilities;
using CounterStrikeSharp.API.Modules.Utils;
using CS2GamingAPIShared;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using static CounterStrikeSharp.API.Core.Listeners;

namespace PlantingExpert
{
    public class Plugin : BasePlugin, IPluginConfig<Configs>
    {
        public override string ModuleName => "The Planting Expert Acheivement";
        public override string ModuleVersion => "1.0";

        private ICS2GamingAPIShared? _cs2gamingAPI { get; set; }
        public static PluginCapability<ICS2GamingAPIShared> _capability { get; } = new("cs2gamingAPI");
        public Configs Config { get; set; } = new Configs();
        public Dictionary<CCSPlayerController, PlayerBombPlantCount> _playerBombCount { get; set; } = new();
        public string? filePath { get; set; }
        public readonly ILogger<Plugin> _logger;

        public override void Load(bool hotReload)
        {
            RegisterListener<OnClientDisconnect>(OnClientDisconnect);
            InitializeData();
        }

        public override void OnAllPluginsLoaded(bool hotReload)
        {
            _cs2gamingAPI = _capability.Get();
        }

        public Plugin(ILogger<Plugin> logger)
        {
            _logger = logger;
        }

        public void OnConfigParsed(Configs config)
        {
            Config = config;
        }

        public void InitializeData()
        {
            filePath = Path.Combine(ModuleDirectory, "playerdata.json");

            if (!File.Exists(filePath))
            {
                var empty = "{}";

                File.WriteAllText(filePath, empty);
                _logger.LogInformation("Data file is not found creating a new one.");
            }

            _logger.LogInformation("Found Data file at {0}.", filePath);
        }

        [GameEventHandler]
        public HookResult OnPlayerConnectFull(EventPlayerConnectFull @event, GameEventInfo info)
        {
            var client = @event.Userid;

            if (!IsValidPlayer(client))
                return HookResult.Continue;

            var steamID = client!.AuthorizedSteamID!.SteamId64;

            var data = GetPlayerData(steamID);


            if (data == null)
                _playerBombCount.Add(client!, new());

            else
            {
                var count = data.BombCount;
                var complete = data.Complete;

                if (data.TimeReset == DateTime.Today.ToShortDateString())
                {
                    count = 0;
                    complete = false;
                    Task.Run(async () => await SaveClientData(steamID, count, complete, true));
                }

                _playerBombCount.Add(client!, new(count, complete));
            }

            return HookResult.Continue;
        }

        public void OnClientDisconnect(int playerslot)
        {
            var client = Utilities.GetPlayerFromSlot(playerslot);

            if (!IsValidPlayer(client))
                return;

            var steamID = client!.AuthorizedSteamID!.SteamId64;
            var complete = _playerBombCount[client].Complete;
            var value = _playerBombCount[client].BombCount;

            Task.Run(async () => await SaveClientData(steamID, value, complete, !complete));

            _playerBombCount.Remove(client!);
        }

        [GameEventHandler]
        public HookResult OnBombPlanted(EventBombPlanted @event, GameEventInfo info)
        {
            var client = @event.Userid;
            AddTimer(0.1f, () => CountBomb(client!));

            return HookResult.Continue;
        }

        public void CountBomb(CCSPlayerController client)
        {
            if (!IsValidPlayer(client))
                return;

            if (Config.MaxBombPlantCount <= 0)
                return;

            if (!_playerBombCount.ContainsKey(client!))
                return;

            if (_playerBombCount[client!].Complete)
                return;

            _playerBombCount[client!].BombCount += 1;

            if (_playerBombCount[client!].BombCount >= Config.MaxBombPlantCount)
            {
                var steamid = client.AuthorizedSteamID?.SteamId64;
                Task.Run(async () => await TaskComplete(client!, (ulong)steamid!));
            }
        }

        public async Task TaskComplete(CCSPlayerController client, ulong steamid)
        {
            if (_playerBombCount[client].Complete)
                return;

            _playerBombCount[client].Complete = true;
            var response = await _cs2gamingAPI?.RequestSteamID(steamid!)!;
            if (response != null)
            {
                if (response.Status != 200)
                    return;

                Server.NextFrame(() =>
                {
                    client.PrintToChat($" {ChatColors.Green}[Acheivement]{ChatColors.Default} You acheive 'The Planting Expert' (Planting the C4 for {Config.MaxBombPlantCount} times)");
                    client.PrintToChat($" {ChatColors.Green}[Acheivement]{ChatColors.Default} {response.Message}");
                });

                await SaveClientData(steamid!, _playerBombCount[client].BombCount, true, true);
            }
        }

        private async Task SaveClientData(ulong steamid, int count, bool complete, bool settime)
        {
            var finishTime = DateTime.Today.ToShortDateString();
            var resetTime = DateTime.Today.AddDays(7.0).ToShortDateString();
            var steamKey = steamid.ToString();

            var data = new PlayerData(finishTime, resetTime, count, complete);

            var jsonObject = ParseFileToJsonObject();

            if (jsonObject == null)
                return;

            if (jsonObject.ContainsKey(steamKey))
            {
                jsonObject[steamKey].BombCount = count;
                jsonObject[steamKey].Complete = complete;

                if (settime)
                {
                    jsonObject[steamKey].TimeAcheived = finishTime;
                    jsonObject[steamKey].TimeReset = resetTime;
                }

                var updated = JsonConvert.SerializeObject(jsonObject, Formatting.Indented);
                await File.WriteAllTextAsync(filePath!, updated);
            }

            else
            {
                jsonObject.Add(steamKey, data);
                var updated = JsonConvert.SerializeObject(jsonObject, Formatting.Indented);
                await File.WriteAllTextAsync(filePath!, updated);
            }
        }

        private PlayerData? GetPlayerData(ulong steamid)
        {
            var jsonObject = ParseFileToJsonObject();

            if (jsonObject == null)
                return null;

            var steamKey = steamid.ToString();

            if (jsonObject.ContainsKey(steamKey))
                return jsonObject[steamKey];

            return null;
        }

        private async void RemovePlayerFromData(ulong steamid)
        {
            var jsonObject = ParseFileToJsonObject();

            if (jsonObject == null)
                return;

            var steamKey = steamid.ToString();

            if (jsonObject.ContainsKey(steamKey))
            {
                _logger.LogInformation("Successfully removed {0} from player data file.", steamKey);
                jsonObject.Remove(steamKey);
                var updated = JsonConvert.SerializeObject(jsonObject, Formatting.Indented);
                await File.WriteAllTextAsync(filePath!, updated);
            }

            else
                _logger.LogError("SteamID {0} is not existed!", steamKey);
        }

        private Dictionary<string, PlayerData>? ParseFileToJsonObject()
        {
            if (!File.Exists(filePath))
                return null;

            return JsonConvert.DeserializeObject<Dictionary<string, PlayerData>>(File.ReadAllText(filePath));
        }

        public bool IsValidPlayer(CCSPlayerController? client)
        {
            return client != null && client.IsValid && !client.IsBot;
        }
    }
}
