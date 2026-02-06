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
        [HarmonyPostfix]
        [HarmonyPatch(typeof(Item), "GetHeldItemInfo")]
        public static void Postfix_Item_Info(Item __instance, ItemSlot inSlot, StringBuilder dsc)
        {
            // Exclude Admin items from any spectral processing
            if (__instance.Code.Path.IndexOf("admin", StringComparison.OrdinalIgnoreCase) >= 0) return;
            
            // 1. DATA COLLECTION
            float spectralBonus = inSlot.Itemstack.ItemAttributes?["spectralDamageBonus"].AsFloat(0f) ?? 0f;
            bool isSpectral = spectralBonus > 0;
            
            // Safe identification for arrows: check class or item code path
            bool isArrow = __instance is ItemArrow || __instance.Code.Path.IndexOf("arrow", StringComparison.OrdinalIgnoreCase) >= 0;
            
            // 2. GUARD CLAUSE
            // Process if it's a Sword, Spear, Arrow or any Spectral item
            if (__instance.Tool != EnumTool.Sword && __instance.Tool != EnumTool.Spear && !isSpectral && !isArrow) return;

            // Prevent duplicate entries
            string label = Lang.Get("spookynights:iteminfo-spectral-attack-power");
            if (dsc.ToString().Contains(label)) return;

            var lines = dsc.ToString().Split(new[] { "\n", "\r\n" }, StringSplitOptions.None).ToList();

            // 3. SCAN AND INJECT
            for (int i = 0; i < lines.Count; i++)
            {
                string line = lines[i];
                
                // Keywords detection (Supports Vanilla and Combat Overhaul)
                bool isDamageLine = line.IndexOf("handed", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    line.IndexOf("power", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    line.IndexOf("Damage", StringComparison.OrdinalIgnoreCase) >= 0 ||
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

            // 4. FOOTERS
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
            else
            {
                // Add malus footer for all non-spectral weapons/arrows caught by the guard clause
                string malusText = Lang.Get("spookynights:iteminfo-spectralmalus");
                if (!lines.Any(l => l.Contains("50%"))) lines.Add(malusText);
            }

            dsc.Clear().Append(string.Join("\n", lines));
        }

        private static void AddStatModifiers(ItemStack stack, List<string> lines)
        {
            if (stack.ItemAttributes == null || !stack.ItemAttributes.KeyExists("statModifiers")) return;

            var mods = stack.ItemAttributes["statModifiers"];
            float walk = mods["walkSpeed"].AsFloat(0f);
            float hunger = mods["hungerrate"].AsFloat(0f);

            if (walk != 0) lines.Add($"<font color=\"{(walk < 0 ? "#ff8080" : "#80ff80")}\">{Lang.Get("spookynights:malus-walkspeed", (walk * 100).ToString("0.#"))}</font>");
            if (hunger != 0) lines.Add($"<font color=\"{(hunger > 0 ? "#ff8080" : "#80ff80")}\">{Lang.Get("spookynights:malus-hungerrate", "+" + (hunger * 100).ToString("0.#"))}</font>");
        }
    }
}