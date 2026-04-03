using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace SpookyNights
{
  [HarmonyPatch]
  public class HarmonyPatches
  {
    [HarmonyPostfix]
    [HarmonyPatch(typeof(Item), "GetHeldItemInfo")]
    public static void Postfix_Item_Info(Item __instance, ItemSlot inSlot, StringBuilder dsc)
    {
      if (__instance.Code == null || inSlot.Itemstack == null) return;
      if (__instance.Code.Path.Contains("admin")) return;

      float spectralBonus = inSlot.Itemstack.ItemAttributes?["spectralDamageBonus"].AsFloat(0f) ?? 0f;
      bool isSpectral = spectralBonus > 0;

      // Comprehensive detection of all weapon types in the mod
      bool isArrow = __instance.Code.Path.Contains("arrow");
      bool isClubOrMace = __instance.Code.Path.Contains("club") || __instance.Code.Path.Contains("mace");
      bool isSlingOrAmmo = __instance.Code.Path.Contains("sling") || __instance.Code.Path.Contains("bullet") ||
                           __instance.Code.Path.Contains("stone") || __instance.Code.Path.Contains("pellet");
      bool isScythe = __instance.Code.Path.Contains("scythe");

      // 2. Filter allowed tools
      if (__instance.Tool != EnumTool.Sword &&
          __instance.Tool != EnumTool.Spear &&
          !isSpectral && !isArrow && !isClubOrMace && !isSlingOrAmmo && !isScythe) return;

      string currentText = dsc.ToString();
      string labelMelee = Lang.Get("spookynights:iteminfo-spectral-attack-power");

      // 3. Early Exit (Anti-Loop Protection)
      if (currentText.IndexOf(labelMelee, StringComparison.OrdinalIgnoreCase) >= 0 ||
          currentText.IndexOf("spookynights:iteminfo-spectral-ranged-damage", StringComparison.OrdinalIgnoreCase) >= 0 ||
          currentText.IndexOf("damage penalty against spectral", StringComparison.OrdinalIgnoreCase) >= 0)
      {
        return;
      }

      var oldLines = currentText.Split(new[] { "\n", "\r\n" }, StringSplitOptions.None);
      var finalLines = new List<string>();
      bool bonusAdded = false;
      string bonusMsg = Lang.Get("spookynights:iteminfo-spectralbonus-simplified", ((spectralBonus - 1) * 100).ToString("0"));

      foreach (var line in oldLines)
      {
        if (string.IsNullOrWhiteSpace(line)) continue;
        if (line.IndexOf("spectral", StringComparison.OrdinalIgnoreCase) >= 0 ||
            line.IndexOf("bonus damage", StringComparison.OrdinalIgnoreCase) >= 0) continue;

        string cleaned = line.Replace("-", "").Replace(" hp", "").Replace("hp", "").Trim();

        // Ranged detection for Slings and Arrows
        bool isRangedStat = line.IndexOf("piercing", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            line.IndexOf("thrown", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            ((isArrow || isSlingOrAmmo) && line.IndexOf("Damage:", StringComparison.OrdinalIgnoreCase) >= 0);

        if (isRangedStat && isSpectral && !bonusAdded && spectralBonus > 1.001f)
        {
          finalLines.Add("");
          finalLines.Add($"<font color=\"#a08ee0\">{bonusMsg}</font>");
          finalLines.Add("");
          bonusAdded = true;
        }

        // isDmgLine will catch "One-handed", "Two-handed", and Ranged stats
        bool isDmgLine = line.IndexOf("handed", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         line.IndexOf("power", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         isRangedStat;

        if (isDmgLine)
        {
          finalLines.Add(cleaned);
          Match m = Regex.Match(line, @"\b\d+([.,]\d+)?\b");
          if (m.Success)
          {
            if (float.TryParse(m.Value.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out float dmg))
            {
              float res = dmg * (isSpectral ? spectralBonus : 0.5f);
              string color = isSpectral ? "#a08ee0" : "#ff8080";
              string specLine = isRangedStat
                  ? Lang.Get("spookynights:iteminfo-spectral-ranged-damage", res.ToString("0.##"))
                  : $"{labelMelee} {res.ToString("0.##")}";

              finalLines.Add($"<font color=\"{color}\">{specLine}</font>");
            }
          }
        }
        else { finalLines.Add(cleaned); }

        // Combat Overhaul range display fix
        if (line.IndexOf("Attack range", StringComparison.OrdinalIgnoreCase) >= 0 ||
            line.IndexOf("MaxReach", StringComparison.OrdinalIgnoreCase) >= 0)
        {
          finalLines.Add("");
        }
      }

      // Footer handling
      if (!bonusAdded)
      {
        finalLines.Add("");
        if (isSpectral)
        {
          if (spectralBonus > 1.001f) finalLines.Add($"<font color=\"#a08ee0\">{bonusMsg}</font>");
          if (inSlot.Itemstack.ItemAttributes?.KeyExists("statModifiers") == true)
          {
            finalLines.Add("");
            AddStatModifiers(inSlot.Itemstack, finalLines);
          }
        }
        else { finalLines.Add($"<font color=\"#ff8080\">{Lang.Get("spookynights:iteminfo-spectralmalus")}</font>"); }
      }
      dsc.Clear().Append(string.Join("\n", finalLines));
    }

    private static void AddStatModifiers(ItemStack stack, List<string> lines)
    {
      var mods = stack.ItemAttributes["statModifiers"];
      float s = mods["walkSpeed"].AsFloat(0f);
      float h = mods["hungerrate"].AsFloat(0f);
      if (s != 0) lines.Add($"<font color=\"{(s < 0 ? "#ff8080" : "#80ff80")}\">{Lang.Get("spookynights:malus-walkspeed", (s * 100).ToString("0.#"))}</font>");
      if (h != 0) lines.Add($"<font color=\"{(h > 0 ? "#ff8080" : "#80ff80")}\">{Lang.Get("spookynights:malus-hungerrate", "+" + (h * 100).ToString("0.#"))}</font>");
    }
  }
}