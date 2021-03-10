using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Workcart Safe Zones", "WhiteThunder", "1.0.0")]
    [Description("Adds safe zones and optional NPC auto turrets to workcarts.")]
    internal class WorkcartSafeZones : CovalencePlugin
    {
        #region Fields

        private static WorkcartSafeZones _pluginInstance;
        private static Configuration _pluginConfig;

        private const string PermissionUse = "workcartsafezones.use";
        private const string BanditSentryPrefab = "assets/content/props/sentry_scientists/sentry.bandit.static.prefab";

        private const float SafeZoneWarningCooldown = 10;

        private Dictionary<ulong, float> _playersLastWarnedTime = new Dictionary<ulong, float>();

        private SavedData _pluginData;

        #endregion

        #region Hooks

        private void Init()
        {
            _pluginInstance = this;
            _pluginData = SavedData.Load();

            permission.RegisterPermission(PermissionUse, this);

            Unsubscribe(nameof(OnEntitySpawned));
        }

        private void Unload()
        {
            SafeCart.DestroyAll();
            _pluginInstance = null;
            _pluginConfig = null;
        }

        private void OnServerInitialized()
        {
            _pluginData.CleanStaleData();

            foreach (var entity in BaseNetworkable.serverEntities)
            {
                var workcart = entity as TrainEngine;
                if (workcart != null && (_pluginConfig.AutoZones || _pluginData.SafeWorkcarts.Contains(workcart.net.ID)))
                    workcart.gameObject.AddComponent<SafeCart>();
            }

            if (_pluginConfig.AutoZones)
                Subscribe(nameof(OnEntitySpawned));
        }

        private void OnEntitySpawned(TrainEngine workcart)
        {
            TryCreateSafeZone(workcart);
        }

        private bool? OnEntityTakeDamage(TrainEngine workcart)
        {
            if (workcart.GetComponent<SafeCart>() != null)
            {
                // Return true (standard) to cancel default behavior (prevent damage).
                return true;
            }

            return null;
        }

        private void OnEntityEnter(TriggerSafeZone triggerSafeZone, BasePlayer player)
        {
            if (player.IsNpc
                || triggerSafeZone.GetComponentInParent<SafeCart>() == null
                || !player.IsHostile())
                return;

            var hostileTimeRemaining = player.State.unHostileTimestamp - Network.TimeEx.currentTimestamp;
            if (hostileTimeRemaining < 0)
                return;

            float lastWarningTime;
            if (_playersLastWarnedTime.TryGetValue(player.userID, out lastWarningTime)
                && lastWarningTime + SafeZoneWarningCooldown > Time.realtimeSinceStartup)
                return;

            ChatMessage(player, "Warning.Hostile", TimeSpan.FromSeconds(Math.Ceiling(hostileTimeRemaining)).ToString("g"));
            _playersLastWarnedTime[player.userID] = Time.realtimeSinceStartup;
        }

        #endregion

        #region API

        private bool API_CreateSafeZone(TrainEngine workcart)
        {
            if (workcart.GetComponent<SafeCart>() != null)
                return true;

            return TryCreateSafeZone(workcart);
        }

        #endregion

        #region Commands

        [Command("safecart.add")]
        private void CommandAddSafeZone(IPlayer player, string cmd, string[] args)
        {
            if (player.IsServer)
                return;

            if (!player.HasPermission(PermissionUse))
            {
                ReplyToPlayer(player, "Error.NoPermission");
                return;
            }

            if (_pluginConfig.AutoZones)
            {
                ReplyToPlayer(player, "Error.AutoZonesEnabled");
                return;
            }

            var basePlayer = player.Object as BasePlayer;
            var workcart = GetPlayerCart(basePlayer);

            if (workcart == null)
            {
                ReplyToPlayer(player, "Error.NoWorkcartFound");
                return;
            }

            if (workcart.GetComponent<SafeCart>() != null)
            {
                ReplyToPlayer(player, "Error.SafeZonePresent");
                return;
            }

            if (TryCreateSafeZone(workcart))
            {
                _pluginData.AddWorkcart(workcart);
                ReplyToPlayer(player, "Add.Success");
            }
        }

        [Command("safecart.remove")]
        private void CommandRemoveSafeZone(IPlayer player, string cmd, string[] args)
        {
            if (player.IsServer)
                return;

            if (!player.HasPermission(PermissionUse))
            {
                ReplyToPlayer(player, "Error.NoPermission");
                return;
            }

            if (_pluginConfig.AutoZones)
            {
                ReplyToPlayer(player, "Error.AutoZonesEnabled");
                return;
            }

            var basePlayer = player.Object as BasePlayer;
            var workcart = GetPlayerCart(basePlayer);

            if (workcart == null)
            {
                ReplyToPlayer(player, "Error.NoWorkcartFound");
                return;
            }

            var component = workcart.GetComponent<SafeCart>();
            if (component == null)
            {
                ReplyToPlayer(player, "Error.NoSafeZone");
                return;
            }

            UnityEngine.Object.Destroy(component);
            _pluginData.RemoveWorkcart(workcart);
            ReplyToPlayer(player, "Remove.Success");
        }

        #endregion

        #region Helper Methods

        private static bool AddSafeZoneWasBlocked(TrainEngine workcart)
        {
            object hookResult = Interface.CallHook("OnWorkcartSafeZoneCreate", workcart);
            return hookResult is bool && (bool)hookResult == false;
        }

        private static bool TryCreateSafeZone(TrainEngine workcart)
        {
            if (AddSafeZoneWasBlocked(workcart))
                return false;

            workcart.gameObject.AddComponent<SafeCart>();
            workcart.SetHealth(workcart.MaxHealth());
            Interface.CallHook("OnWorkcartSafeZoneCreated", workcart);

            return true;
        }

        private static NPCAutoTurret SpawnTurret(BaseEntity entity, Vector3 position, float rotationAngle)
        {
            var rotation = rotationAngle == 0 ? Quaternion.identity : Quaternion.Euler(0, rotationAngle, 0);

            var autoTurret = GameManager.server.CreateEntity(BanditSentryPrefab, position, rotation) as NPCAutoTurret;
            if (autoTurret == null)
                return null;

            autoTurret.enableSaving = false;
            autoTurret.SetParent(entity);
            autoTurret.Spawn();

            return autoTurret;
        }

        private static BaseEntity GetLookEntity(BasePlayer player, float maxDistance = 6)
        {
            RaycastHit hit;
            return Physics.Raycast(player.eyes.HeadRay(), out hit, maxDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                ? hit.GetEntity()
                : null;
        }

        private static TrainEngine GetMountedCart(BasePlayer player)
        {
            var mountedWorkcart = player.GetMountedVehicle() as TrainEngine;
            if (mountedWorkcart != null)
                return mountedWorkcart;

            var parentWorkcart = player.GetParentEntity() as TrainEngine;
            if (parentWorkcart != null)
                return parentWorkcart;

            return null;
        }

        private static TrainEngine GetPlayerCart(BasePlayer player) =>
            GetLookEntity(player) as TrainEngine ?? GetMountedCart(player);

        #endregion

        #region Safe Zone

        internal class SafeCart : MonoBehaviour
        {
            public static void DestroyAll()
            {
                foreach (var entity in BaseNetworkable.serverEntities)
                {
                    var workcart = entity as TrainEngine;
                    if (workcart == null)
                        continue;

                    var component = workcart.GetComponent<SafeCart>();
                    if (component == null)
                        continue;

                    Destroy(component);
                }
            }

            private TrainEngine _workcart;
            private GameObject _child;
            private List<TriggerSafeZone> _safeZones = new List<TriggerSafeZone>();
            private List<NPCAutoTurret> _autoTurrets = new List<NPCAutoTurret>();

            private void Awake()
            {
                _workcart = GetComponent<TrainEngine>();
                if (_workcart == null)
                    return;

                AddPrimarySafeZones();
                AddVolumetricSafeZone();
                MaybeAddTurrets();
            }

            private void MaybeAddTurrets()
            {
                if (!_pluginConfig.EnableTurrets)
                    return;

                foreach (var turretConfig in _pluginConfig.TurretPositions)
                    _autoTurrets.Add(SpawnTurret(_workcart, turretConfig.Position, turretConfig.RotationAngle));
            }

            private void AddPrimarySafeZones()
            {
                // Add a trigger alongside each TriggerParent (and implicitly TriggerLadder).
                // This ensures that boarding the workcart will add the player to the safe zone.
                foreach (var triggerParent in GetComponentsInChildren<TriggerParent>())
                {
                    var safeZone = triggerParent.gameObject.AddComponent<TriggerSafeZone>();
                    safeZone.interestLayers = Rust.Layers.Mask.Player_Server;
                    _safeZones.Add(safeZone);
                }
            }

            private void AddVolumetricSafeZone()
            {
                _child = gameObject.CreateChild();

                var safeZone = _child.AddComponent<TriggerSafeZone>();
                safeZone.interestLayers = Rust.Layers.Mask.Player_Server;

                var radius = _pluginConfig.SafeZoneRadius;
                if (radius > 0)
                {
                    var collider = _child.gameObject.AddComponent<SphereCollider>();
                    collider.isTrigger = true;
                    collider.gameObject.layer = 18;
                    collider.center = Vector3.zero;
                    collider.radius = radius;
                }
                else
                {
                    // Add a box collider for just the workcart area.
                    // This fixes an issue where dismounting the cabin would remove you from the safe zone.
                    var collider = _child.gameObject.AddComponent<BoxCollider>();
                    collider.isTrigger = true;
                    collider.gameObject.layer = 18;
                    collider.size = _workcart.bounds.extents * 2 + Vector3.up * 6;
                }
            }

            private void OnDestroy()
            {
                foreach (var triggerSafeZone in _safeZones)
                    if (triggerSafeZone != null)
                        Destroy(triggerSafeZone);

                if (_child != null)
                    Destroy(_child);

                foreach (var autoTurret in _autoTurrets)
                    if (autoTurret != null)
                        autoTurret.Kill();
            }
        }

        #endregion

        #region Saved Data

        private class SavedData
        {
            [JsonProperty("SafeWorkcartIds")]
            public List<uint> SafeWorkcarts = new List<uint>();

            public static SavedData Load() =>
                Interface.Oxide.DataFileSystem.ReadObject<SavedData>(_pluginInstance.Name) ?? new SavedData();

            public void Save() =>
                Interface.Oxide.DataFileSystem.WriteObject<SavedData>(_pluginInstance.Name, this);

            public void AddWorkcart(TrainEngine workcart)
            {
                SafeWorkcarts.Add(workcart.net.ID);
                Save();
            }

            public void RemoveWorkcart(TrainEngine workcart)
            {
                SafeWorkcarts.Remove(workcart.net.ID);
                Save();
            }

            public void CleanStaleData()
            {
                var cleanedCount = 0;

                for (var i = SafeWorkcarts.Count - 1; i >= 0; i--)
                {
                    var entity = BaseNetworkable.serverEntities.Find(SafeWorkcarts[i]);
                    if (entity == null)
                    {
                        SafeWorkcarts.RemoveAt(i);
                        cleanedCount++;
                    }
                }

                if (cleanedCount > 0)
                    Save();
            }
        }

        #endregion

        #region Configuration

        private class Configuration : SerializableConfiguration
        {
            [JsonProperty("AutoZones")]
            public bool AutoZones = false;

            [JsonProperty("SafeZoneRadius")]
            public float SafeZoneRadius = 0;

            [JsonProperty("EnableTurrets")]
            public bool EnableTurrets = true;

            [JsonProperty("TurretPositions")]
            public TurretConfig[] TurretPositions = new TurretConfig[]
            {
                new TurretConfig
                {
                    Position = new Vector3(0.85f, 2.62f, 1.25f),
                    RotationAngle = 180,
                },
                new TurretConfig
                {
                    Position = new Vector3(0.7f, 3.84f, 3.7f)
                }
            };
        }

        private class TurretConfig
        {
            [JsonProperty("Position")]
            public Vector3 Position;

            [JsonProperty("RotationAngle")]
            public float RotationAngle;
        }

        private Configuration GetDefaultConfig() => new Configuration();

        #endregion

        #region Configuration Boilerplate

        private class SerializableConfiguration
        {
            public string ToJson() => JsonConvert.SerializeObject(this);

            public Dictionary<string, object> ToDictionary() => JsonHelper.Deserialize(ToJson()) as Dictionary<string, object>;
        }

        private static class JsonHelper
        {
            public static object Deserialize(string json) => ToObject(JToken.Parse(json));

            private static object ToObject(JToken token)
            {
                switch (token.Type)
                {
                    case JTokenType.Object:
                        return token.Children<JProperty>()
                                    .ToDictionary(prop => prop.Name,
                                                  prop => ToObject(prop.Value));

                    case JTokenType.Array:
                        return token.Select(ToObject).ToList();

                    default:
                        return ((JValue)token).Value;
                }
            }
        }

        private bool MaybeUpdateConfig(SerializableConfiguration config)
        {
            var currentWithDefaults = config.ToDictionary();
            var currentRaw = Config.ToDictionary(x => x.Key, x => x.Value);
            return MaybeUpdateConfigDict(currentWithDefaults, currentRaw);
        }

        private bool MaybeUpdateConfigDict(Dictionary<string, object> currentWithDefaults, Dictionary<string, object> currentRaw)
        {
            bool changed = false;

            foreach (var key in currentWithDefaults.Keys)
            {
                object currentRawValue;
                if (currentRaw.TryGetValue(key, out currentRawValue))
                {
                    var defaultDictValue = currentWithDefaults[key] as Dictionary<string, object>;
                    var currentDictValue = currentRawValue as Dictionary<string, object>;

                    if (defaultDictValue != null)
                    {
                        if (currentDictValue == null)
                        {
                            currentRaw[key] = currentWithDefaults[key];
                            changed = true;
                        }
                        else if (MaybeUpdateConfigDict(defaultDictValue, currentDictValue))
                            changed = true;
                    }
                }
                else
                {
                    currentRaw[key] = currentWithDefaults[key];
                    changed = true;
                }
            }

            return changed;
        }

        protected override void LoadDefaultConfig() => _pluginConfig = GetDefaultConfig();

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _pluginConfig = Config.ReadObject<Configuration>();
                if (_pluginConfig == null)
                {
                    throw new JsonException();
                }

                if (MaybeUpdateConfig(_pluginConfig))
                {
                    LogWarning("Configuration appears to be outdated; updating and saving");
                    SaveConfig();
                }
            }
            catch
            {
                LogWarning($"Configuration file {Name}.json is invalid; using defaults");
                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig()
        {
            Log($"Configuration changes saved to {Name}.json");
            Config.WriteObject(_pluginConfig, true);
        }

        #endregion

        #region Localization

        private void ReplyToPlayer(IPlayer player, string messageName, params object[] args) =>
            player.Reply(string.Format(GetMessage(player, messageName), args));

        private void ChatMessage(BasePlayer player, string messageName, params object[] args) =>
            player.ChatMessage(string.Format(GetMessage(player.IPlayer, messageName), args));

        private string GetMessage(IPlayer player, string messageName, params object[] args) =>
            GetMessage(player.Id, messageName, args);

        private string GetMessage(string playerId, string messageName, params object[] args)
        {
            var message = lang.GetMessage(messageName, this, playerId);
            return args.Length > 0 ? string.Format(message, args) : message;
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Error.NoPermission"] = "You don't have permission to do that.",
                ["Error.NoWorkcartFound"] = "Error: No workcart found.",
                ["Error.AutoZonesEnabled"] = "Error: You cannot do that while automatic zones are enabled.",
                ["Error.SafeZonePresent"] = "That workcart already has a safe zone.",
                ["Error.NoSafeZone"] = "That workcart doesn't have a safe zone.",
                ["Add.Success"] = "Successfully added safe zone to the workcart.",
                ["Remove.Success"] = "Successfully removed safe zone from the workcart.",
                ["Warning.Hostile"] = "You are <color=red>hostile</color> for <color=red>{0}</color>. No safe zone protection.",
            }, this, "en");
        }

        #endregion
    }
}
