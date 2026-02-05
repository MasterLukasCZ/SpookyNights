using HarmonyLib;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.GameContent;

namespace SpookyNights
{
    [HarmonyPatch]
    public class HarmonyPatches
    {
        // --- PATCH 1: MELEE WEAPONS ---
        // Covers: Swords, Vanilla Spears, Warscythe, and any modded weapon (CO) with spectral attributes.
        [HarmonyPostfix]
        [HarmonyPatch(typeof(Item), "GetHeldItemInfo")]
        public static void Postfix_Item_Melee(Item __instance, ItemSlot inSlot, StringBuilder dsc)
        {
            // 1. Detection: Is this a Spectral Weapon? (Checks Attribute "spectralDamageBonus")
            float spectralBonus = inSlot.Itemstack.ItemAttributes?["spectralDamageBonus"].AsFloat(0f) ?? 0f;
            bool isSpectral = spectralBonus > 0;

            // 2. Filter: Only process if it is a Sword, Spear, or a recognized Spectral Weapon
            if (__instance.Tool != EnumTool.Sword && __instance.Tool != EnumTool.Spear && !isSpectral) return;

            // 3. Guard: Prevent duplicates
            string spectralPowerText = Lang.Get("spookynights:iteminfo-spectral-attack-power");
            if (dsc.ToString().Contains(spectralPowerText)) return;

            float baseMeleeDamage = __instance.GetAttackPower(inSlot.Itemstack);

            if (baseMeleeDamage > 0)
            {
                var lines = dsc.ToString().Split('\n').ToList();

                // 4. Find the line with the damage number
                string numStrDot = baseMeleeDamage.ToString("0.#", CultureInfo.InvariantCulture);
                string numStrComma = baseMeleeDamage.ToString("0.#", CultureInfo.GetCultureInfo("fr-FR"));
                int meleeIndex = lines.FindIndex(line => line.Contains(numStrDot) || line.Contains(numStrComma));

                if (meleeIndex != -1)
                {
                    // 5. Logic: 
                    // - If Spectral: Show Bonus (Purple)
                    // - If Vanilla: Show Penalty (Red)
                    float damageValue = isSpectral ? (baseMeleeDamage * spectralBonus) : (baseMeleeDamage * 0.5f);
                    string colorCode = isSpectral ? "#a08ee0" : "#ff8080"; 
                    
                    // Note: If Vanilla, we display negative value implicitly by calculation, 
                    // but usually we want to show "Spectral Power: X hp". 
                    // If it is a penalty, it represents the *effective* damage against ghosts.
                    string spectralLine = $"<font color=\"{colorCode}\">{spectralPowerText}-{damageValue:0.##} hp</font>";
                    lines.Insert(meleeIndex + 1, spectralLine);
                }

                // 6. Footer Logic
                if (isSpectral)
                {
                    // Show Bonus Percentage Footer (e.g. +20% Damage)
                    if (spectralBonus > 1.001f)
                    {
                        string bonusText = Lang.Get("spookynights:iteminfo-spectralbonus-simplified", ((spectralBonus - 1) * 100).ToString("0"));
                        if (!lines.Contains(bonusText)) lines.Add(bonusText);
                    }

                    // Show Stat Modifiers (Walk Speed / Hunger) - ported from ItemSpectralWeapon
                    if (inSlot.Itemstack.ItemAttributes != null && inSlot.Itemstack.ItemAttributes.KeyExists("statModifiers"))
                    {
                        AddStatModifiers(inSlot.Itemstack, lines);
                    }
                }
                else if (__instance.Tool == EnumTool.Sword)
                {
                    // Vanilla: Show Malus Explanation
                    string malusText = Lang.Get("spookynights:iteminfo-spectralmalus");
                    if (!lines.Any(l => l.Contains("50%"))) lines.Add(malusText);
                }

                dsc.Clear().Append(string.Join("\n", lines));
            }
        }

        // --- PATCH 2: RANGED SPEARS ---
        [HarmonyPostfix]
        [HarmonyPatch(typeof(ItemSpear), "GetHeldItemInfo")]
        public static void Postfix_Spear_Ranged(ItemSpear __instance, ItemSlot inSlot, StringBuilder dsc)
        {
            // 1. Detection
            float spectralBonus = inSlot.Itemstack.ItemAttributes?["spectralDamageBonus"].AsFloat(0f) ?? 0f;
            bool isSpectral = spectralBonus > 0;

            // 2. Guard
            string uniqueKey = Lang.Get("spookynights:iteminfo-spectral-ranged-damage", "").Trim();
            string checkStr = uniqueKey.Split(':')[0];
            if (dsc.ToString().Contains(checkStr)) return;

            var lines = dsc.ToString().Split('\n').ToList();
            string vanillaRangedFormat = Lang.Get("itemdescriptor-projectile-damage").Replace("{0}", "").Trim();
            if (string.IsNullOrEmpty(vanillaRangedFormat)) vanillaRangedFormat = "piercing";

            int rangedIndex = lines.FindIndex(line => line.Contains(vanillaRangedFormat));

            if (rangedIndex != -1)
            {
                Match match = Regex.Match(lines[rangedIndex], @"\d+([.,]\d+)?");
                if (match.Success)
                {
                    string numStr = match.Value.Replace(',', '.');
                    if (float.TryParse(numStr, NumberStyles.Any, CultureInfo.InvariantCulture, out float baseThrownDamage))
                    {
                        float damageValue = isSpectral ? (baseThrownDamage * spectralBonus) : (baseThrownDamage * 0.5f);
                        string colorCode = isSpectral ? "#a08ee0" : "#ff8080";

                        string rangedLabel = Lang.Get("spookynights:iteminfo-spectral-ranged-damage", damageValue.ToString("0.##"));
                        lines.Insert(rangedIndex + 1, $"<font color=\"{colorCode}\">{rangedLabel}</font>");
                    }
                }
            }

            // Footer Logic (Vanilla Only) - Spectral footer is handled in Melee patch usually
            if (!isSpectral)
            {
                string malusText = Lang.Get("spookynights:iteminfo-spectralmalus");
                if (!lines.Any(l => l.Contains("50%"))) lines.Add(malusText);
            }

            dsc.Clear().Append(string.Join("\n", lines));
        }

        // --- PATCH 3: ARROWS ---
        [HarmonyPostfix]
        [HarmonyPatch(typeof(ItemArrow), "GetHeldItemInfo")]
        public static void Postfix_Arrow(ItemArrow __instance, ItemSlot inSlot, StringBuilder dsc)
        {
            // 1. Detection
            float spectralBonus = inSlot.Itemstack.ItemAttributes?["spectralDamageBonus"].AsFloat(0f) ?? 0f;
            bool isSpectral = spectralBonus > 0;

            // 2. Guard
            string uniqueKey = Lang.Get("spookynights:iteminfo-spectral-ranged-damage", "").Trim();
            string checkStr = uniqueKey.Split(':')[0];
            if (dsc.ToString().Contains(checkStr)) return;

            float baseDamage = __instance.Attributes?["damage"].AsFloat(0f) ?? 0f;
            // Also support "projectileDamage" attribute which some mods use
            if (baseDamage == 0) baseDamage = __instance.Attributes?["projectileDamage"].AsFloat(0f) ?? 0f;

            if (baseDamage > 0)
            {
                float damageValue = isSpectral ? (baseDamage * spectralBonus) : (baseDamage * 0.5f);
                string colorCode = isSpectral ? "#a08ee0" : "#ff8080";

                string rangedLabel = Lang.Get("spookynights:iteminfo-spectral-ranged-damage", damageValue.ToString("0.##"));
                string spectralLine = $"<font color=\"{colorCode}\">{rangedLabel}</font>";

                var lines = dsc.ToString().Split('\n').ToList();
                string numStrDot = baseDamage.ToString(CultureInfo.InvariantCulture);
                string numStrComma = baseDamage.ToString(CultureInfo.GetCultureInfo("fr-FR"));

                int index = lines.FindLastIndex(line => line.Contains(numStrDot) || line.Contains(numStrComma));
                
                if (index != -1) lines.Insert(index + 1, spectralLine);
                else lines.Add(spectralLine);

                // Footer Logic
                if (isSpectral)
                {
                    if (spectralBonus > 1.001f)
                    {
                        string bonusText = Lang.Get("spookynights:iteminfo-spectralbonus-simplified", ((spectralBonus - 1) * 100).ToString("0"));
                        if (!lines.Contains(bonusText)) lines.Add(bonusText);
                    }
                }
                else
                {
                    string malusText = Lang.Get("spookynights:iteminfo-spectralmalus");
                    if (!lines.Any(l => l.Contains("50%"))) lines.Add(malusText);
                }

                dsc.Clear().Append(string.Join("\n", lines));
            }
        }

        // --- HELPER METHODS ---

        private static void AddStatModifiers(ItemStack stack, List<string> lines)
        {
            var mods = stack.ItemAttributes["statModifiers"];
            float walkMalus = mods["walkSpeed"].AsFloat(0f);
            float hungerMalus = mods["hungerrate"].AsFloat(0f);

            if (walkMalus != 0)
            {
                string color = walkMalus < 0 ? "#ff8080" : "#80ff80"; // Red if negative speed
                string valStr = (walkMalus * 100).ToString("0.#");
                string text = Lang.Get("spookynights:malus-walkspeed", valStr);
                lines.Add($"<font color=\"{color}\">{text}</font>");
            }
            if (hungerMalus != 0)
            {
                string color = hungerMalus > 0 ? "#ff8080" : "#80ff80"; // Red if positive (hunger increases faster)
                string valStr = "+" + (hungerMalus * 100).ToString("0.#");
                string text = Lang.Get("spookynights:malus-hungerrate", valStr);
                lines.Add($"<font color=\"{color}\">{text}</font>");
            }
        }
    }
}