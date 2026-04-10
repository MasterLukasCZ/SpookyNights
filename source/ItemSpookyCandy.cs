using System;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace SpookyNights
{
    public class ItemSpookyCandy : Item
    {
        private static readonly Random rand = new Random();

        // --- TOOLTIP DISPLAY ---
        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);
            if (inSlot.Itemstack == null) return;

            string candyType = GetCandyType(inSlot.Itemstack);
            string descText = "";
            string color = "#DDDDDD";

            switch (candyType)
            {
                case "ghostcaramel":
                    descText = Lang.Get("spookynights:candy-desc-ghostcaramel");
                    // Matches particle color (Gold/Caramel)
                    color = "#D79637";
                    break;
                case "mummy":
                    descText = Lang.Get("spookynights:candy-desc-mummy");
                    // Matches particle color (Off-White/Grey)
                    color = "#D7D2D2";
                    break;
                case "spidergummy":
                    descText = Lang.Get("spookynights:candy-desc-spidergummy");
                    // Matches particle color (Toxic Green)
                    color = "#77DD77";
                    break;
                case "vampireteeth":
                    descText = Lang.Get("spookynights:candy-desc-vampireteeth");
                    // Matches particle color (Blood Red)
                    color = "#FF6666";
                    break;
                case "shadowcube":
                    descText = Lang.Get("spookynights:candy-desc-shadowcube");
                    // Matches particle hue (Dark Purple)
                    color = "#9F5F9F";
                    break;
            }

            if (!string.IsNullOrEmpty(descText))
            {
                dsc.AppendLine($"\n<font color=\"{color}\">{descText}</font>");
            }
        }

        // --- INTERACTION HANDLER ---
        public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            base.OnHeldInteractStop(secondsUsed, slot, byEntity, blockSel, entitySel);
            if (slot.Itemstack == null) return;

            if (secondsUsed > 0.9f)
            {
                string candyType = GetCandyType(slot.Itemstack);
          
                // --- CLIENT SIDE: PHYSICS (Jump) ---
                if (api.Side == EnumAppSide.Client)
                {
                    if (candyType == "spidergummy")
                    {
                        // Physics impulse
                        byEntity.Pos.Motion.Y += 0.35;
                        Vec3f look = byEntity.Pos.GetViewVector();
                        byEntity.Pos.Motion.X += look.X * 0.5;
                        byEntity.Pos.Motion.Z += look.Z * 0.5;
                    }
                }

                // --- SERVER SIDE: STATS & EFFECTS ---
                if (api.Side == EnumAppSide.Server)
                {
                    ApplyCandyEffect(byEntity, slot.Itemstack, candyType);
                }
            }
        }

        private void ApplyCandyEffect(EntityAgent entity, ItemStack stack, string candyType)
        {
            if (entity is not EntityPlayer playerEntity) return;
            IServerPlayer? serverPlayer = playerEntity.Player as IServerPlayer;
            if (serverPlayer == null) return;

            // Default color just in case
            int particleColor = ColorUtil.ToRgba(255, 200, 255, 255);

            switch (candyType)
            {
                case "ghostcaramel":
                    // Bonus: Stability
                    double currentStab = entity.WatchedAttributes.GetDouble("temporalStability");
                    entity.WatchedAttributes.SetDouble("temporalStability", Math.Min(1.5, currentStab + 0.25));

                    // Malus: Weakness (30s)
                    entity.Stats.Set("meleeWeaponsDamage", "candy_weakness", -0.5f, false);

                    api.World.RegisterCallback((dt) => {
                        entity.Stats.Remove("meleeWeaponsDamage", "candy_weakness");
                        serverPlayer.SendMessage(GlobalConstants.GeneralChatGroup, Lang.Get("spookynights:candy-expire-weakness"), EnumChatType.Notification);
                    }, 30000);

                    serverPlayer.SendMessage(GlobalConstants.GeneralChatGroup, Lang.Get("spookynights:candy-msg-ghostcaramel"), EnumChatType.Notification);

                    // Color: Gold/Beige (A:200, R:215, G:150, B:55)
                    particleColor = ColorUtil.ToRgba(200, 215, 150, 55);
                    break;

                case "mummy":
                    // Bonus: Heal +2
                    entity.ReceiveDamage(new DamageSource() { Type = EnumDamageType.Heal }, 2f);

                    // Bonus: Knockback Resistance
                    entity.Stats.Set("knockbackResistance", "candy_anchor", 1.0f, false);

                    // Malus: Slowness
                    entity.Stats.Set("walkspeed", "candy_slow", -0.25f, false);

                    api.World.RegisterCallback((dt) => {
                        entity.Stats.Remove("walkspeed", "candy_slow");
                        entity.Stats.Remove("knockbackResistance", "candy_anchor");
                        serverPlayer.SendMessage(GlobalConstants.GeneralChatGroup, Lang.Get("spookynights:candy-expire-slow"), EnumChatType.Notification);
                    }, 45000);

                    serverPlayer.SendMessage(GlobalConstants.GeneralChatGroup, Lang.Get("spookynights:candy-msg-mummy"), EnumChatType.Notification);

                    // Color: White/Grey (A:200, R:215, G:210, B:210)
                    particleColor = ColorUtil.ToRgba(200, 215, 210, 210);
                    break;

                case "spidergummy":
                    // Bonus: No Fall Damage
                    entity.Stats.Set("fallDamageMultiplier", "candy_feather", -1.0f, false);
                    api.World.RegisterCallback((dt) => {
                        entity.Stats.Remove("fallDamageMultiplier", "candy_feather");
                    }, 10000);

                    // Malus: Poison
                    api.World.RegisterCallback((dt) => {
                        entity.ReceiveDamage(new DamageSource() { Source = EnumDamageSource.Internal, Type = EnumDamageType.Poison }, 1f);
                    }, 1000);
                    api.World.RegisterCallback((dt) => {
                        entity.ReceiveDamage(new DamageSource() { Source = EnumDamageSource.Internal, Type = EnumDamageType.Poison }, 1f);
                    }, 3000);

                    serverPlayer.SendMessage(GlobalConstants.GeneralChatGroup, Lang.Get("spookynights:candy-msg-spidergummy"), EnumChatType.Notification);

                    // Color: Toxic Green (A:200, R:30, G:255, B:30)
                    particleColor = ColorUtil.ToRgba(200, 30, 255, 30);
                    break;

                case "vampireteeth":
                    // Bonus: Heal +6
                    entity.ReceiveDamage(new DamageSource() { Type = EnumDamageType.Heal }, 6f);

                    // MALUS: Temporal Instability (-30%)
                    double vampStab = entity.WatchedAttributes.GetDouble("temporalStability", 1.0);
                    double newStab = Math.Max(0.0, vampStab - 0.3);
                    entity.WatchedAttributes.SetDouble("temporalStability", newStab);

                    serverPlayer.SendMessage(GlobalConstants.GeneralChatGroup, Lang.Get("spookynights:candy-msg-vampireteeth"), EnumChatType.Notification);

                    // Color: Blood Red (A:200, R:205, G:0, B:0)
                    particleColor = ColorUtil.ToRgba(200, 205, 0, 0);
                    break;

                case "shadowcube":
                    ApplyShadowChaos(entity, serverPlayer);

                    // Color: Dark Orange/Brown/Purple (A:200, R:70, G:20, B:85)
                    particleColor = ColorUtil.ToRgba(200, 70, 20, 85);
                    break;
            }

            // --- CRITICAL: This triggers the "eating" particles for ALL candies ---
            SpawnEatingParticles(entity, particleColor);
        }

        private void ApplyShadowChaos(EntityAgent entity, IServerPlayer serverPlayer)
        {
            int roll = rand.Next(0, 100);
            string msg = "";

            if (roll < 5) // DIVINE
            {
                entity.ReceiveDamage(new DamageSource() { Type = EnumDamageType.Heal }, 20f);
                FillAllSatiety(entity);
                entity.WatchedAttributes.SetDouble("temporalStability", 1.5);
                msg = Lang.Get("spookynights:candy-msg-shadow-divine");
            }
            else if (roll < 25) // SPEED
            {
                entity.Stats.Set("walkspeed", "candy_speed", 0.5f, false);
                api.World.RegisterCallback((dt) => {
                    entity.Stats.Remove("walkspeed", "candy_speed");
                    serverPlayer.SendMessage(GlobalConstants.GeneralChatGroup, Lang.Get("spookynights:candy-expire-speed"), EnumChatType.Notification);
                }, 45000);
                msg = Lang.Get("spookynights:candy-msg-shadow-speed");
            }
            else if (roll < 55) // HALLUCINATION
            {
                entity.WatchedAttributes.SetFloat("intoxication", 0.8f);
                entity.World.PlaySoundAt(new AssetLocation("spookynights:sounds/creature/specter_growls"), entity);
                msg = Lang.Get("spookynights:candy-msg-shadow-hallucination");
            }
            else if (roll < 80) // DARKNESS
            {
                entity.Stats.Set("walkspeed", "candy_heavy_slow", -0.5f, false);
                RegisterParticleLoop(entity, 15, ColorUtil.ToRgba(255, 0, 0, 0)); // Black particles
                api.World.RegisterCallback((dt) => {
                    entity.Stats.Remove("walkspeed", "candy_heavy_slow");
                }, 15000);
                msg = Lang.Get("spookynights:candy-msg-shadow-blind");
            }
            else // TELEPORT FAIL
            {
                double dx = (rand.NextDouble() - 0.5) * 20;
                double dz = (rand.NextDouble() - 0.5) * 20;
                entity.TeleportToDouble(entity.Pos.X + dx, entity.Pos.Y + 1, entity.Pos.Z + dz);

                Block? web = entity.World.GetBlock(new AssetLocation("game:cobweb-n-d"));
                if (web != null) entity.World.BlockAccessor.SetBlock(web.Id, entity.Pos.AsBlockPos);

                msg = Lang.Get("spookynights:candy-msg-shadow-fail");
            }

            serverPlayer.SendMessage(GlobalConstants.GeneralChatGroup, msg, EnumChatType.Notification);
        }

        // --- HELPERS ---

        private string GetCandyType(ItemStack stack)
        {
            if (stack == null) return "";
            string candyType = stack.Attributes.GetString("type", "");
            if (string.IsNullOrEmpty(candyType))
            {
                string[] parts = this.Code.Path.Split('-');
                if (parts.Length > 1) candyType = parts[1];
            }
            return candyType;
        }

        private void FillAllSatiety(EntityAgent entity)
        {
            ITreeAttribute hungerTree = entity.WatchedAttributes.GetTreeAttribute("hunger");
            if (hungerTree == null) return;
            hungerTree.SetFloat("saturation", 1500f);
            string[] categories = new string[] { "fruit", "vegetable", "grain", "protein", "dairy" };
            foreach (string cat in categories) hungerTree.SetFloat(cat, 1500f);
            entity.WatchedAttributes.MarkPathDirty("hunger");
        }

        private void RegisterParticleLoop(EntityAgent entity, int durationSeconds, int color)
        {
            long id = 0;
            int tickCount = 0;
            id = entity.Api.World.RegisterGameTickListener((dt) =>
            {
                if (tickCount >= durationSeconds * 10)
                {
                    entity.Api.World.UnregisterGameTickListener(id);
                    return;
                }
                SpawnCloudParticles(entity, color);
                tickCount++;
            }, 100);
        }

        private void SpawnCloudParticles(EntityAgent entity, int color)
        {
            SimpleParticleProperties particles = new SimpleParticleProperties(
               8, 12,
               color,
               new Vec3d(), new Vec3d(),
               new Vec3f(-0.5f, -0.5f, -0.5f),
               new Vec3f(0.5f, 0.5f, 0.5f),
               1.0f,
               0f,
               1.0f, 3.0f,
               EnumParticleModel.Quad
           );
            particles.MinPos = entity.Pos.XYZ.AddCopy(-1, entity.LocalEyePos.Y - 0.5, -1);
            particles.AddPos = new Vec3d(2, 1, 2);
            particles.WithTerrainCollision = false;
            particles.VertexFlags = 0;
            entity.World.SpawnParticles(particles);
        }

        private void SpawnEatingParticles(EntityAgent entity, int color)
        {
            SimpleParticleProperties particles = new SimpleParticleProperties(
                10, 15,
                color,
                new Vec3d(), new Vec3d(),
                new Vec3f(-0.2f, 0.1f, -0.2f),
                new Vec3f(0.2f, 0.5f, 0.2f),
                1.0f,
                -0.02f,
                0.2f, 0.5f,
                EnumParticleModel.Quad
            );
            particles.MinPos = entity.Pos.XYZ.AddCopy(-0.3, 1.0, -0.3);
            particles.AddPos = new Vec3d(0.6, 0.5, 0.6);
            particles.WithTerrainCollision = false;
            particles.VertexFlags = 255;
            particles.OpacityEvolve = EvolvingNatFloat.create(EnumTransformFunction.LINEAR, -255);
            entity.World.SpawnParticles(particles);
        }
    }
}