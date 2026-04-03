using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace SpookyNights
{
  public class ItemCandyBag : Item
  {
    private static readonly Random rand = new Random();

    public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot)
    {
      return new WorldInteraction[] {
                new WorldInteraction()
                {
                    ActionLangCode = "spookynights:heldhelp-openbag",
                    MouseButton = EnumMouseButton.Right,
                    ShouldApply = (wi, bs, es) => {
                        var clientApi = api as ICoreClientAPI;
                        // Added safety: check if player and entity exist
                        if (clientApi?.World?.Player?.Entity == null) return false;
                        return !clientApi.World.Player.Entity.Controls.Sneak;
                    }
                }
            }.Append(base.GetHeldInteractionHelp(inSlot));
    }

    public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstTick, ref EnumHandHandling handHandling)
    {
      if (byEntity.Controls.Sneak)
      {
        base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstTick, ref handHandling);
        return;
      }

      handHandling = EnumHandHandling.Handled;
    }

    public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
    {
      if (byEntity.Controls.Sneak) return false;

      // Security fix for CS8602: check if Itemstack is null
      if (slot.Itemstack == null) return false;

      float useDelay = slot.Itemstack.Attributes.GetFloat("useDelay", 0.5f);
      return secondsUsed < useDelay;
    }

    public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
    {
      if (byEntity.Controls.Sneak) return;

      // Security fix for CS8602: check if Itemstack is null
      if (slot.Itemstack == null) return;

      float useDelay = slot.Itemstack.Attributes.GetFloat("useDelay", 0.5f);
      if (secondsUsed < useDelay) return;

      if (api.Side != EnumAppSide.Server) return;

      if (byEntity is EntityPlayer entityPlayer)
      {
        slot.TakeOut(1);
        slot.MarkDirty();
        GiveRandomCandy(entityPlayer.Player);
      }
    }

    private void GiveRandomCandy(IPlayer byPlayer)
    {
      int amount = rand.Next(1, 3);

      for (int i = 0; i < amount; i++)
      {
        string candyCode = GetWeightedRandomCandy();
        // Fix for CS8600: declared as nullable Item?
        Item? candyItem = api.World.GetItem(new AssetLocation("spookynights", candyCode));

        if (candyItem != null)
        {
          ItemStack candyStack = new ItemStack(candyItem, 1);

          if (!byPlayer.InventoryManager.TryGiveItemstack(candyStack))
          {
            api.World.SpawnItemEntity(candyStack, byPlayer.Entity.Pos.XYZ);
          }
        }
      }

      api.World.PlaySoundAt(new AssetLocation("game:sounds/player/collect"), byPlayer.Entity);
    }

    private string GetWeightedRandomCandy()
    {
      double roll = rand.NextDouble();

      if (roll < 0.25) return "spookycandy-spidergummy";
      if (roll < 0.50) return "spookycandy-mummy";
      if (roll < 0.70) return "spookycandy-ghostcaramel";
      if (roll < 0.85) return "spookycandy-vampireteeth";

      return "spookycandy-shadowcube";
    }
  }
}