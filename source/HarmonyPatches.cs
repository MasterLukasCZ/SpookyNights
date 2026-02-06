using HarmonyLib;
using System;
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
        // --- UNIVERSAL MELEE PATCH (Swords, Spears, Falx) ---
        [HarmonyPostfix]
        [HarmonyPatch(typeof(Item), "GetHeldItemInfo")]
        public static void Postfix_Item_Melee(Item __instance, ItemSlot inSlot, StringBuilder dsc)
        {
            float spectralBonus = inSlot.Itemstack.ItemAttributes?["spectralDamageBonus"].AsFloat(0f) ?? 0f;
            bool isSpectral = spectralBonus > 0;

            if (__instance.Tool != EnumTool.Sword && __instance.Tool != EnumTool.Spear && !isSpectral) return;

            string label = Lang.Get("spookynights:iteminfo-spectral-attack-power");
            if (dsc.ToString().Contains(label)) return;

            var lines = dsc.ToString().Split(new[] { "\n", "\r\n" }, StringSplitOptions.None).ToList();

            for (int i = 0; i < lines.Count; i++)
            {
                string line = lines[i];
                
                // Identify damage lines
                bool isDamageLine = line.IndexOf("handed", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    line.IndexOf("power", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    (line.IndexOf("Damage:", StringComparison.OrdinalIgnoreCase) >= 0 && __instance.Tool != EnumTool.Spear) ||
                                    line.Contains("tier)");

                if (isDamageLine)
                {
                    Match match = Regex.Match(line, @"-?\d+([.,]\d+)?");
                    if (match.Success)
                    {
                        string valStr = match.Value.Replace(',', '.');
                        if (float.TryParse(valStr, NumberStyles.Any, CultureInfo.InvariantCulture, out float dmgVal))
                        {
                            float spectralVal = Math.Abs(dmgVal) * (isSpectral ? spectralBonus : 0.5f);
                            string color = isSpectral ? "#a08ee0" : "#ff8080";
                            
                            lines.Insert(i + 1, $"<font color=\"{color}\">{label} {spectralVal:0.##}</font>");
                            i++; 
                        }
                    }
                }
            }

            // --- FOOTERS ---
            if (isSpectral)
            {
                if (spectralBonus > 1.001f)
                {
                    string bonusText = Lang.Get("spookynights:iteminfo-spectralbonus-simplified", ((spectralBonus - 1) * 100).ToString("0"));
                    if (!lines.Contains(bonusText)) lines.Add(bonusText);
                }

                if (inSlot.Itemstack.ItemAttributes != null && inSlot.Itemstack.ItemAttributes.KeyExists("statModifiers"))
                {
                    AddStatModifiers(inSlot.Itemstack, lines);
                }
            }
            else if (__instance.Tool == EnumTool.Sword || __instance.Tool == EnumTool.Spear)
            {
                string malusText = Lang.Get("spookynights:iteminfo-spectralmalus");
                if (!lines.Any(l => l.Contains("50%"))) lines.Add(malusText);
            }

            dsc.Clear().Append(string.Join("\n", lines));
        }

        // --- AMMO / PROJECTILE PATCH ---
        [HarmonyPostfix]
        [HarmonyPatch(typeof(ItemArrow), "GetHeldItemInfo")]
        public static void Postfix_Arrow(ItemArrow __instance, ItemSlot inSlot, StringBuilder dsc)
        {
            float bonus = inSlot.Itemstack.ItemAttributes?["spectralDamageBonus"].AsFloat(0f) ?? 0f;
            bool isSpectral = bonus > 0;
            string labelKey = Lang.Get("spookynights:iteminfo-spectral-ranged-damage", "").Trim().Split(':')[0];
            
            if (dsc.ToString().Contains(labelKey)) return;

            var lines = dsc.ToString().Split(new[] { "\n", "\r\n" }, StringSplitOptions.None).ToList();
            
            for (int i = 0; i < lines.Count; i++)
            {
                if (lines[i].Contains("damage") || lines[i].Contains("projectile") || lines[i].Contains("Knockback"))
                {
                    Match m = Regex.Match(lines[i], @"-?\d+([.,]\d+)?");
                    if (m.Success)
                    {
                        float val = Math.Abs(float.Parse(m.Value.Replace(',', '.'), CultureInfo.InvariantCulture));
                        float res = val * (isSpectral ? bonus : 0.5f);
                        string color = isSpectral ? "#a08ee0" : "#ff8080";
                        string text = Lang.Get("spookynights:iteminfo-spectral-ranged-damage", res.ToString("0.##"));
                        lines.Insert(i + 1, $"<font color=\"{color}\">{text}</font>");
                        break; 
                    }
                }
            }
            
            if (!isSpectral)
            {
                string malus = Lang.Get("spookynights:iteminfo-spectralmalus");
                if (!lines.Any(l => l.Contains("50%"))) lines.Add(malus);
            }
            
            dsc.Clear().Append(string.Join("\n", lines));
        }

        private static void AddStatModifiers(ItemStack stack, List<string> lines)
        {
            var mods = stack.ItemAttributes["statModifiers"];
            float walkMalus = mods["walkSpeed"].AsFloat(0f);
            float hungerMalus = mods["hungerrate"].AsFloat(0f);

            if (walkMalus != 0) 
            {
                string color = walkMalus < 0 ? "#ff8080" : "#80ff80"; 
                lines.Add($"<font color=\"{color}\">{Lang.Get("spookynights:malus-walkspeed", (walkMalus * 100).ToString("0.#"))}</font>");
            }
            if (hungerMalus != 0) 
            {
                string color = hungerMalus > 0 ? "#ff8080" : "#80ff80"; 
                lines.Add($"<font color=\"{color}\">{Lang.Get("spookynights:malus-hungerrate", "+" + (hungerMalus * 100).ToString("0.#"))}</font>");
            }
        }
    }
}