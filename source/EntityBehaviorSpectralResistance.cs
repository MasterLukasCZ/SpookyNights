using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

namespace SpookyNights
{
    public class EntityBehaviorSpectralResistance : EntityBehavior
    {
        private float resistance;

        public EntityBehaviorSpectralResistance(Entity entity) : base(entity) { }

        public override void Initialize(EntityProperties properties, JsonObject attributes)
        {
            base.Initialize(properties, attributes);
            this.resistance = attributes["resistance"].AsFloat(0.5f);
        }

        public override void OnEntityReceiveDamage(DamageSource damageSource, ref float damage)
        {
            ItemStack? sourceStack = null;

            // 1. Identify source item
            if (damageSource.SourceEntity is EntityAgent agent)
            {
                sourceStack = agent.RightHandItemSlot?.Itemstack;
            }
            else if (damageSource.SourceEntity is EntityProjectile projectile)
            {
                sourceStack = projectile.ProjectileStack;
            }

            // 2. If no item found (fall damage, empty hand, etc.) -> Apply base resistance
            if (sourceStack == null)
            {
                damage *= this.resistance;
                return;
            }

            // 3. BYPASS: If it's an admin tool, exit without modifying damage
            // We use ?. to safely access Collectible and Code
            if (sourceStack.Collectible?.Code?.Path?.IndexOf("admin", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return;
            }

            // 4. Extract Spectral Bonus
            // We check Attributes safely to satisfy the compiler (Warning CS8602)
            float spectralBonus = 0f;
            if (sourceStack.Collectible?.Attributes != null)
            {
                // Get from stack attributes first, fallback to collectible attributes
                spectralBonus = sourceStack.Attributes.GetFloat("spectralDamageBonus",
                    sourceStack.Collectible.Attributes["spectralDamageBonus"].AsFloat(0f));
            }

            // 5. Apply Logic
            if (spectralBonus > 0f)
            {
                // Spectral weapon: Apply its own multiplier
                damage *= spectralBonus;
            }
            else
            {
                // Non-spectral weapon: Apply creature resistance (penalty)
                damage *= this.resistance;
            }
        }

        public override string PropertyName() => "spectralresistance";
    }
}