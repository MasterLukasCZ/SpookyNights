using HarmonyLib;
using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace SpookyNights
{
    public struct CandyDropDefinition
    {
        public float Chance;
        public int MinQuantity;
        public int MaxQuantity;

        public CandyDropDefinition(float chance, int min, int max)
        {
            Chance = chance;
            MinQuantity = min;
            MaxQuantity = max;
        }
    }

    public sealed class SpookyNightsModSystem : ModSystem
    {
        private ICoreAPI api = default!;
        private ICoreServerAPI sapi = default!;
        private List<string> spectralCreatureCodes = new List<string>();

        // --- HARDCODED LOOT TABLE ---
        private static readonly Dictionary<string, CandyDropDefinition> LootTable = new Dictionary<string, CandyDropDefinition>()
        {
            // Weak
            { "spookynights:spectraldrifter-normal",      new CandyDropDefinition(0.15f, 1, 1) },
            { "spookynights:spectraldrifter-deep",        new CandyDropDefinition(0.20f, 1, 1) },
            // Medium
            { "spookynights:spectraldrifter-tainted",     new CandyDropDefinition(0.30f, 1, 1) },
            { "spookynights:spectralshiver-surface",      new CandyDropDefinition(0.25f, 1, 1) },
            { "spookynights:spectralshiver-deep",         new CandyDropDefinition(0.30f, 1, 1) },
            { "spookynights:spectralbowtorn-surface",     new CandyDropDefinition(0.25f, 1, 1) },
            { "spookynights:spectralwolf-eurasian-adult-*", new CandyDropDefinition(0.30f, 1, 1) },
            // Strong
            { "spookynights:spectraldrifter-corrupt",     new CandyDropDefinition(0.40f, 1, 2) },
            { "spookynights:spectraldrifter-nightmare",   new CandyDropDefinition(0.50f, 1, 2) },
            { "spookynights:spectralshiver-tainted",      new CandyDropDefinition(0.35f, 1, 2) },
            { "spookynights:spectralshiver-corrupt",      new CandyDropDefinition(0.40f, 2, 3) },
            { "spookynights:spectralshiver-nightmare",    new CandyDropDefinition(0.60f, 3, 5) },
            { "spookynights:spectralbowtorn-deep",        new CandyDropDefinition(0.35f, 1, 2) },
            { "spookynights:spectralbowtorn-tainted",     new CandyDropDefinition(0.40f, 2, 3) },
            { "spookynights:spectralbowtorn-corrupt",     new CandyDropDefinition(0.45f, 2, 4) },
            { "spookynights:spectralbear-brown-adult-*",  new CandyDropDefinition(0.50f, 1, 2) },
            // Elites
            { "spookynights:spectraldrifter-double-headed", new CandyDropDefinition(0.80f, 2, 3) },
            { "spookynights:spectralbowtorn-nightmare",     new CandyDropDefinition(0.65f, 3, 5) },
            { "spookynights:spectralbowtorn-gearfoot",      new CandyDropDefinition(0.75f, 4, 6) },
            { "spookynights:spectralshiver-stilt",          new CandyDropDefinition(0.70f, 4, 6) },
            { "spookynights:spectralshiver-bellhead",       new CandyDropDefinition(0.70f, 4, 6) },
            { "spookynights:spectralshiver-deepsplit",      new CandyDropDefinition(0.70f, 4, 6) },
            // Bosses
            { "spookynights:spectralbear-giant-adult-*",    new CandyDropDefinition(1.00f, 3, 5) }
        };

        // --- LIFECYCLE ---

        public override void StartPre(ICoreAPI api)
        {
            this.api = api;
            if (api.Side.IsServer())
            {
                ConfigManager.LoadServerConfig(api);
                // Safety: Clamp light threshold
                if (ConfigManager.ServerConf != null)
                {
                    if (ConfigManager.ServerConf.LightLevelThreshold < 0) ConfigManager.ServerConf.LightLevelThreshold = 0;
                    if (ConfigManager.ServerConf.LightLevelThreshold > 32) ConfigManager.ServerConf.LightLevelThreshold = 32;
                }
            }
            if (api.Side.IsClient()) { ConfigManager.LoadClientConfig(api); }
        }

        public override void AssetsFinalize(ICoreAPI api)
        {
            if (api.Side.IsClient())
            {
                if (ConfigManager.ClientConf != null && !ConfigManager.ClientConf.EnableJackOLanternParticles)
                {
                    string[] variants = { "north", "east", "south", "west" };
                    foreach (string variant in variants)
                    {
                        AssetLocation blockCode = new AssetLocation("spookynights", "jackolantern-" + variant);
                        Block? block = api.World.GetBlock(blockCode);
                        if (block != null) { block.ParticleProperties = null; }
                    }
                }
            }
        }

        public override void Start(ICoreAPI api)
        {
            base.Start(api);
            new Harmony("fr.laerinok.spookynights").PatchAll();
            
            api.RegisterEntityBehaviorClass("proximitywarning", typeof(EntityBehaviorProximityWarning));
            
            // Classes for items removed to support Harmony injection & Compatibility
            api.RegisterItemClass("ItemCandyBag", typeof(ItemCandyBag));
            api.RegisterItemClass("ItemSpookyCandy", typeof(ItemSpookyCandy));

            api.RegisterEntityBehaviorClass("spectralresistance", typeof(EntityBehaviorSpectralResistance));
            api.RegisterEntityBehaviorClass("spectralhandling", typeof(EntityBehaviorSpectralHandling));

            if (api.Side.IsServer())
            {
                api.Logger.Notification("🌟 Spooky Nights (Server) is loaded!");
            }
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            api.Event.OnEntitySpawn += AddPlayerBehavior;
            api.Event.OnEntityLoaded += AddPlayerBehavior;
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            this.sapi = api;

            if (ConfigManager.ServerConf != null)
            {
                sapi.Logger.Notification("[SpookyNights] Config loaded.");
            }

            // --- COMMANDS ---

            // /snlight : Check light levels with color coding
            api.ChatCommands.Create("snlight")
                .WithDescription("Displays light levels at player position")
                .RequiresPrivilege(Privilege.chat)
                .HandleWith((args) =>
                {
                    try
                    {
                        var player = args.Caller.Player;
                        if (player == null) return TextCommandResult.Error("Player only.");

                        BlockPos pos = player.Entity.Pos.AsBlockPos;
                        int threshold = (ConfigManager.ServerConf != null) ? ConfigManager.ServerConf.LightLevelThreshold : 14;

                        int sun = api.World.BlockAccessor.GetLightLevel(pos, EnumLightLevelType.TimeOfDaySunLight);
                        int block = api.World.BlockAccessor.GetLightLevel(pos, EnumLightLevelType.OnlyBlockLight);
                        int total = Math.Max(sun, block);

                        string statusColor = (total <= threshold)
                            ? "<font color='#00FF00'>YES (Safe)</font>"
                            : "<font color='#FF0000'>NO (Too Bright)</font>";

                        string message = $"[SN-Debug] Pos: {pos}\n" +
                                         $"Light Total: <strong>{total}</strong> (Max: {threshold}) -> {statusColor}\n" +
                                         $"Details: Sun/Moon={sun}, Block={block}";

                        return TextCommandResult.Success(message);
                    }
                    catch (Exception ex)
                    {
                        return TextCommandResult.Error($"Internal error: {ex.Message}");
                    }
                });

            // /snmoon : Check current moon phase with red alert for Full Moon
            api.ChatCommands.Create("snmoon")
                .WithDescription("Displays current moon phase")
                .RequiresPrivilege(Privilege.chat)
                .HandleWith((args) =>
                {
                    var calendar = api.World.Calendar;
                    string color = (calendar.MoonPhase == EnumMoonPhase.Full) ? "#FF0000" : "#FFFFFF";
                    string message = $"[SN-Debug] Current Moon: <font color='{color}'><strong>{calendar.MoonPhase}</strong></font>";
                    return TextCommandResult.Success(message);
                });

            // Events
            api.Event.OnEntityDeath += OnEntityDeath;

            // "The Doorman" : Checks conditions BEFORE spawn
            api.Event.OnTrySpawnEntity += OnTrySpawnEntity;

            api.Event.OnEntityLoaded += OnEntityLoaded;

            spectralCreatureCodes = new List<string> { "spectraldrifter", "spectralbowtorn", "spectralshiver", "spectralwolf", "spectralbear" };

            // "Cinderella" : Periodic check (Daylight & Boss Moon logic)
            api.Event.RegisterGameTickListener(OnDaylightCheck, 5000);
        }

        // --- HANDLERS ---

        private void OnEntityLoaded(Entity entity)
        {
            if (entity is EntityPlayer && !entity.HasBehavior("spectralhandling"))
            {
                entity.AddBehavior(new EntityBehaviorSpectralHandling(entity));
            }
        }

        private void AddPlayerBehavior(Entity entity)
        {
            if (entity is EntityPlayer && !entity.HasBehavior("spectralhandling"))
            {
                entity.AddBehavior(new EntityBehaviorSpectralHandling(entity));
            }
        }

        // --- DEATH & LOOT ---

        private void OnEntityDeath(Entity entity, DamageSource damageSource)
        {
            if (damageSource == null) return;
            HandleCandyLoot(entity, damageSource);
            if (IsSpectralCreature(entity.Code))
            {
                HandleSpectralDeath(entity);
            }
        }

        private void HandleSpectralDeath(Entity entity)
        {
            int particleColor = GetEntityGlowColor(entity);
            SpawnSpectralParticles(entity.Pos.XYZ, particleColor);

            if (entity.Properties.Attributes?["spectralDrops"]?.Token is JObject dropTable)
            {
                foreach (var entry in dropTable)
                {
                    if (WildcardUtil.Match(entry.Key, entity.Code.Path))
                    {
                        if (entry.Value is JArray drops)
                        {
                            foreach (var dropToken in drops)
                            {
                                TrySpawnDrop(dropToken, entity.Pos.XYZ);
                            }
                        }
                        break;
                    }
                }
            }

            entity.Die(EnumDespawnReason.Expire);
        }

        private int GetEntityGlowColor(Entity entity)
        {
            int defaultColor = ColorUtil.ToRgba(150, 0, 255, 255);
            try
            {
                JsonObject? mainAttrs = entity.Properties.Attributes;
                if (mainAttrs == null) return defaultColor;

                if (mainAttrs.KeyExists("glowColor"))
                {
                    JToken? token = mainAttrs["glowColor"]?.Token;
                    if (token != null && token.Type == JTokenType.String)
                        return ParseHexColor(token.ToString());
                    else if (token is JObject glowTable)
                        return ParseGlowTable(glowTable, entity, defaultColor);
                }

                if (mainAttrs.KeyExists("glowColorByType") && mainAttrs["glowColorByType"]?.Token is JObject glowTableByType)
                {
                    return ParseGlowTable(glowTableByType, entity, defaultColor);
                }
            }
            catch { }
            return defaultColor;
        }

        private int ParseHexColor(string hexColor)
        {
            int rgb = ColorUtil.Hex2Int(hexColor);
            int r = (rgb >> 16) & 0xFF;
            int g = (rgb >> 8) & 0xFF;
            int b = rgb & 0xFF;
            return ColorUtil.ToRgba(150, r, g, b);
        }

        private int ParseGlowTable(JObject glowTable, Entity entity, int defaultColor)
        {
            foreach (var entry in glowTable)
            {
                if (WildcardUtil.Match(entry.Key, entity.Code.Path))
                {
                    string? hexColor = entry.Value?.ToString();
                    if (string.IsNullOrEmpty(hexColor)) return defaultColor;
                    return ParseHexColor(hexColor);
                }
            }
            return defaultColor;
        }

        private void SpawnSpectralParticles(Vec3d pos, int color)
        {
            SimpleParticleProperties particles = new SimpleParticleProperties(
                15, 25,
                color,
                new Vec3d(), new Vec3d(),
                new Vec3f(-0.3f, 0f, -0.3f),
                new Vec3f(0.3f, 1.0f, 0.3f),
                2.0f,
                -0.05f,
                0.5f, 1.5f,
                EnumParticleModel.Quad
            );

            particles.MinPos = pos.AddCopy(-0.5, 0.2, -0.5);
            particles.AddPos = new Vec3d(1, 1.0, 1);
            particles.WithTerrainCollision = false;
            particles.VertexFlags = 200;
            particles.OpacityEvolve = EvolvingNatFloat.create(EnumTransformFunction.LINEAR, -255);
            particles.SizeEvolve = EvolvingNatFloat.create(EnumTransformFunction.LINEAR, 0.5f);
            sapi.World.SpawnParticles(particles);
        }

        private void TrySpawnDrop(JToken dropToken, Vec3d pos)
        {
            try
            {
                JObject? drop = dropToken as JObject;
                if (drop == null) return;

                string? code = drop["code"]?.ToString();
                string? type = drop["type"]?.ToString();
                JToken? quantity = drop["quantity"];

                if (string.IsNullOrEmpty(code)) return;

                float avg = quantity?["avg"]?.ToObject<float>() ?? 1f;
                float var = quantity?["var"]?.ToObject<float>() ?? 0f;
                float finalQuantity = avg + ((float)sapi.World.Rand.NextDouble() * var) - (var / 2f);

                int stackSize = (int)finalQuantity;
                if (sapi.World.Rand.NextDouble() < (finalQuantity - stackSize)) stackSize++;

                if (stackSize <= 0) return;

                ItemStack? stack = null;
                if (type == "item")
                {
                    Item? item = sapi.World.GetItem(new AssetLocation(code));
                    if (item != null)
                    {
                        if (item is ItemStackRandomizer randItem)
                        {
                            ItemStack tempStack = new ItemStack(item, 1);
                            DummySlot dummySlot = new DummySlot(tempStack);
                            randItem.Resolve(dummySlot, sapi.World, true);
                            stack = dummySlot.Itemstack;
                        }
                        else
                        {
                            stack = new ItemStack(item, stackSize);
                        }
                        if (stack != null) stack.StackSize = stackSize;
                    }
                }
                else
                {
                    Block? block = sapi.World.GetBlock(new AssetLocation(code));
                    if (block != null) stack = new ItemStack(block, stackSize);
                }
                if (stack != null) sapi.World.SpawnItemEntity(stack, pos);
            }
            catch (Exception ex)
            {
                sapi.Logger.Warning("[SpookyNights] Failed to spawn custom drop: " + ex.Message);
            }
        }

        private void HandleCandyLoot(Entity entity, DamageSource damageSource)
        {
            if (sapi == null || ConfigManager.ServerConf == null || !ConfigManager.ServerConf.EnableCandyLoot) return;

            var allowedMonths = ConfigManager.ServerConf.AllowedCandyMonths;
            if (allowedMonths != null && allowedMonths.Count > 0)
            {
                int currentMonth = sapi.World.Calendar.Month;
                if (!allowedMonths.Contains(currentMonth)) return;
            }

            if (ConfigManager.ServerConf.CandyOnlyOnFullMoon)
            {
                if (sapi.World.Calendar.MoonPhase != EnumMoonPhase.Full) return;
            }

            if (damageSource.SourceEntity is not EntityPlayer) return;

            CandyDropDefinition? dropDef = null;
            foreach (var entry in LootTable)
            {
                if (WildcardUtil.Match(new AssetLocation(entry.Key), entity.Code))
                {
                    dropDef = entry.Value;
                    break;
                }
            }
            if (dropDef == null) return;
            if (sapi.World.Rand.NextDouble() >= dropDef.Value.Chance) return;

            int amount = sapi.World.Rand.Next(dropDef.Value.MinQuantity, dropDef.Value.MaxQuantity + 1);
            if (amount <= 0) return;

            ItemStack stack = new ItemStack(sapi.World.GetItem(new AssetLocation("spookynights", "candybag")), amount);
            sapi.World.SpawnItemEntity(stack, entity.Pos.XYZ);
        }

        // --- THE DOORMAN: SPAWNING LOGIC ---

        private bool OnTrySpawnEntity(IBlockAccessor blockAccessor, ref EntityProperties properties, Vec3d spawnPosition, long herdId)
        {
            if (ConfigManager.ServerConf == null || properties.Code.Domain != "spookynights") return true;
            var conf = ConfigManager.ServerConf;
            if (!conf.UseTimeBasedSpawning) return true;

            string entityCodeStr = properties.Code.ToString();

            // 1. BOSS LOGIC
            foreach (var bossEntry in conf.Bosses)
            {
                if (WildcardUtil.Match(bossEntry.Key, entityCodeStr))
                {
                    BossSpawningConfig bossConfig = bossEntry.Value;
                    if (bossConfig == null || !bossConfig.Enabled) return false;

                    // FIX: If AllowedMoonPhases is empty, we ALLOW SPAWN (return true if no other constraints)
                    // We only check phases if the list has entries.
                    if (bossConfig.AllowedMoonPhases != null && bossConfig.AllowedMoonPhases.Count > 0)
                    {
                        string currentPhase = sapi.World.Calendar.MoonPhase.ToString();
                        bool moonValid = false;

                        foreach (var allowed in bossConfig.AllowedMoonPhases)
                        {
                            if (string.Equals(allowed, currentPhase, StringComparison.OrdinalIgnoreCase))
                            {
                                moonValid = true;
                                break;
                            }
                        }

                        if (!moonValid) return false;
                    }
                    break;
                }
            }

            // 2. STANDARD LOGIC
            bool isBoss = false;
            foreach (var k in conf.Bosses.Keys) { if (WildcardUtil.Match(k, entityCodeStr)) isBoss = true; }

            if (!isBoss)
            {
                if (conf.SpawnOnlyOnFullMoon && sapi.World.Calendar.MoonPhase != EnumMoonPhase.Full) return false;

                if (conf.SpawnOnlyAtNight)
                {
                    BlockPos pos = spawnPosition.AsBlockPos;
                    int sun = sapi.World.BlockAccessor.GetLightLevel(pos, EnumLightLevelType.TimeOfDaySunLight);
                    int block = sapi.World.BlockAccessor.GetLightLevel(pos, EnumLightLevelType.OnlyBlockLight);
                    if (Math.Max(sun, block) > conf.LightLevelThreshold) return false;
                }

                if (conf.AllowedSpawnMonths != null && conf.AllowedSpawnMonths.Count > 0)
                {
                    if (!conf.AllowedSpawnMonths.Contains(sapi.World.Calendar.Month)) return false;
                }
                if (conf.SpawnOnlyOnLastDayOfMonth)
                {
                    int currentDay = (int)(sapi.World.Calendar.TotalDays % sapi.World.Calendar.DaysPerMonth) + 1;
                    if (currentDay != sapi.World.Calendar.DaysPerMonth) return false;
                }
            }

            // 3. MULTIPLIERS
            float multiplier = 1.0f;
            foreach (var pair in conf.SpawnMultipliers)
            {
                if (WildcardUtil.Match(pair.Key, entityCodeStr))
                {
                    multiplier = pair.Value;
                    break;
                }
            }

            if (!isBoss && !conf.SpawnOnlyOnFullMoon && sapi.World.Calendar.MoonPhase == EnumMoonPhase.Full)
            {
                multiplier *= conf.FullMoonSpawnMultiplier;
            }

            if (multiplier <= 0.0f) return false;
            if (multiplier >= 1.0f) return true;

            return sapi.World.Rand.NextDouble() <= multiplier;
        }

        // --- PERIODIC CHECK (Daylight & Boss Moon) ---

        private void OnDaylightCheck(float dt)
        {
            if (ConfigManager.ServerConf == null) return;
            var conf = ConfigManager.ServerConf;
            if (!conf.UseTimeBasedSpawning) return; // Skip if mechanics disabled

            int threshold = conf.LightLevelThreshold;
            bool manualMode = string.Equals(conf.NightTimeMode, "Manual", StringComparison.OrdinalIgnoreCase);
            string currentMoonPhase = sapi.World.Calendar.MoonPhase.ToString();

            foreach (var entity in sapi.World.LoadedEntities.Values)
            {
                if (IsSpectralCreature(entity.Code))
                {
                    bool shouldDie = false;
                    string entityCodeStr = entity.Code.ToString();

                    // 1. CHECK BOSS CONDITIONS (Moon Phase)
                    // We check this here so even if spawn was forced (egg), it dies later if wrong moon.
                    foreach (var bossEntry in conf.Bosses)
                    {
                        if (WildcardUtil.Match(bossEntry.Key, entityCodeStr))
                        {
                            BossSpawningConfig bossConfig = bossEntry.Value;
                            if (bossConfig != null && bossConfig.Enabled)
                            {
                                // FIX: Logic update here as well. If list empty = Allow All.
                                if (bossConfig.AllowedMoonPhases != null && bossConfig.AllowedMoonPhases.Count > 0)
                                {
                                    bool moonValid = false;
                                    foreach (var allowed in bossConfig.AllowedMoonPhases)
                                    {
                                        if (string.Equals(allowed, currentMoonPhase, StringComparison.OrdinalIgnoreCase))
                                        {
                                            moonValid = true;
                                            break;
                                        }
                                    }

                                    if (!moonValid) shouldDie = true;
                                }
                            }
                            break;
                        }
                    }

                    // 2. CHECK DAYLIGHT (Only if not already dying)
                    if (!shouldDie && conf.SpawnOnlyAtNight)
                    {
                        if (manualMode)
                        {
                            double hour = sapi.World.Calendar.HourOfDay;
                            float start = conf.NightStartHour;
                            float end = conf.NightEndHour;
                            bool isNight = (start > end) ? (hour >= start || hour < end) : (hour >= start && hour < end);

                            if (!isNight) shouldDie = true;
                        }
                        else
                        {
                            BlockPos pos = entity.Pos.AsBlockPos;
                            int sunLight = sapi.World.BlockAccessor.GetLightLevel(pos, EnumLightLevelType.TimeOfDaySunLight);
                            int blockLight = sapi.World.BlockAccessor.GetLightLevel(pos, EnumLightLevelType.OnlyBlockLight);
                            int actualLight = Math.Max(sunLight, blockLight);

                            if (actualLight > threshold) shouldDie = true;
                        }
                    }

                    // EXECUTE DEATH
                    if (shouldDie)
                    {
                        sapi.World.SpawnParticles(10,
                            ColorUtil.ToRgba(100, 100, 100, 100),
                            entity.Pos.XYZ, entity.Pos.XYZ.AddCopy(0, 1, 0),
                            new Vec3f(-0.5f, 0, -0.5f), new Vec3f(0.5f, 1, 0.5f),
                            1.0f, 1.0f);

                        entity.Die(EnumDespawnReason.Expire);
                    }
                }
            }
        }

        private bool IsSpectralCreature(AssetLocation entityCode)
        {
            if (entityCode == null || entityCode.Domain != "spookynights") return false;
            foreach (var code in spectralCreatureCodes)
            {
                if (entityCode.Path.StartsWith(code)) return true;
            }
            return false;
        }
    }
}