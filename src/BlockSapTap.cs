using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace SapTap
{
    public class BlockSapTap : Block
    {
        private const int MaxTapsPerTree = 2;

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling)
        {
            if (blockSel == null) return;
            
            IPlayer byPlayer = (byEntity as EntityPlayer)?.Player;
            if (byPlayer == null) return;

            IWorldAccessor world = byEntity.Api.World;
            Block clickedBlock = world.BlockAccessor.GetBlock(blockSel.Position);
            
            // Log on server side
            if (world.Side == EnumAppSide.Server)
            {
                world.Api.Logger.Notification($"[SapTap OnHeldInteractStart] Clicked on: {clickedBlock?.Code}");
            }

            // Check if it's a resin log
            if (!IsValidSapSource(clickedBlock))
            {
                // Not a valid target, let default behavior happen
                return;
            }

            // Check if tree already has max taps
            if (GetTapCountOnTree(world, blockSel.Position) >= MaxTapsPerTree)
            {
                if (world.Side == EnumAppSide.Client)
                {
                    (world.Api as ICoreClientAPI)?.ShowChatMessage("This tree already has the maximum number of taps.");
                }
                handHandling = EnumHandHandling.PreventDefault;
                return;
            }

            // Try to place the tap
            BlockPos tapPos = blockSel.Position.AddCopy(blockSel.Face);
            Block blockAtPos = world.BlockAccessor.GetBlock(tapPos);
            
            if (blockAtPos.Id != 0 && !blockAtPos.IsReplacableBy(this))
            {
                // Not air/replaceable
                return;
            }

            // Get the correct orientation and ensure it's the empty variant
            string orientation = blockSel.Face.Opposite.Code;
            Block orientedBlock = world.GetBlock(CodeWithVariants(new string[] { "side", "state" }, new string[] { orientation, "empty" }));
            
            if (orientedBlock == null)
            {
                orientedBlock = this;
            }

            // Place the block
            world.BlockAccessor.SetBlock(orientedBlock.BlockId, tapPos);
            world.BlockAccessor.TriggerNeighbourBlockUpdate(tapPos);
            
            // Remove one tap from player's inventory
            slot.TakeOut(1);
            slot.MarkDirty();
            
            if (world.Side == EnumAppSide.Server)
            {
                world.Api.Logger.Notification($"[SapTap] Placed tap at {tapPos}");
            }

            // Prevent the resin harvesting interaction
            handHandling = EnumHandHandling.PreventDefault;
        }

        public override bool CanPlaceBlock(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ref string failureCode)
        {
            // Allow placement attempt - we'll validate in TryPlaceBlock
            Block clickedBlock = world.BlockAccessor.GetBlock(blockSel.Position);
            
            if (world.Side == EnumAppSide.Server)
            {
                world.Api.Logger.Notification($"[SapTap CanPlaceBlock] Clicked on: {clickedBlock?.Code}");
            }
            
            return true;
        }

        public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref string failureCode)
        {
            // Check if we're clicking on a valid resin log
            Block clickedBlock = world.BlockAccessor.GetBlock(blockSel.Position);
            
            // Debug output
            if (world.Side == EnumAppSide.Server)
            {
                world.Api.Logger.Notification($"[SapTap] Clicked on: {clickedBlock?.Code}");
            }

            // Check if it's a pine tree log with resin
            if (!IsValidSapSource(clickedBlock))
            {
                if (world.Side == EnumAppSide.Server)
                {
                    world.Api.Logger.Notification($"[SapTap] Not a valid sap source");
                }
                failureCode = "requiressappinelog";
                return false;
            }

            if (world.Side == EnumAppSide.Server)
            {
                world.Api.Logger.Notification($"[SapTap] Valid sap source!");
            }

            // Check if tree already has max taps
            if (GetTapCountOnTree(world, blockSel.Position) >= MaxTapsPerTree)
            {
                failureCode = "treehasmaxtaps";
                return false;
            }

            // Determine where to place the tap (air adjacent to the log)
            BlockPos tapPos = blockSel.Position.AddCopy(blockSel.Face);
            
            // Check if that position is air/replaceable
            Block blockAtPos = world.BlockAccessor.GetBlock(tapPos);
            if (blockAtPos.Id != 0 && !blockAtPos.IsReplacableBy(this))
            {
                failureCode = "notair";
                return false;
            }

            // Get the correct orientation based on which face was clicked
            string orientation = blockSel.Face.Opposite.Code; // Tap faces away from the wall
            Block orientedBlock = world.GetBlock(CodeWithVariant("side", orientation));
            
            if (orientedBlock == null)
            {
                if (world.Side == EnumAppSide.Server)
                {
                    world.Api.Logger.Warning($"[SapTap] Could not find variant with orientation: {orientation}");
                }
                orientedBlock = this;
            }

            // Place the block
            world.BlockAccessor.SetBlock(orientedBlock.BlockId, tapPos);
            world.BlockAccessor.TriggerNeighbourBlockUpdate(tapPos);
            
            if (world.Side == EnumAppSide.Server)
            {
                world.Api.Logger.Notification($"[SapTap] Placed tap at {tapPos}");
            }

            return true;
        }

        private bool IsValidSapSource(Block block)
        {
            if (block == null) return false;

            // Check if it's a leaking pine log with resin
            string blockCode = block.Code.ToString().ToLower();
            
            // Must be a pine log AND have resin
            if (blockCode.Contains("log") && blockCode.Contains("pine") && blockCode.Contains("resin"))
            {
                return true;
            }

            return false;
        }

        private int GetTapCountOnTree(IWorldAccessor world, BlockPos treePos)
        {
            int tapCount = 0;
            HashSet<BlockPos> checkedPositions = new HashSet<BlockPos>();
            Queue<BlockPos> toCheck = new Queue<BlockPos>();
            
            toCheck.Enqueue(treePos);
            checkedPositions.Add(treePos);

            // Search connected tree blocks
            while (toCheck.Count > 0 && checkedPositions.Count < 100) // Limit search size
            {
                BlockPos currentPos = toCheck.Dequeue();
                Block currentBlock = world.BlockAccessor.GetBlock(currentPos);

                // Check all adjacent blocks for taps
                foreach (BlockFacing facing in BlockFacing.ALLFACES)
                {
                    BlockPos adjacentPos = currentPos.AddCopy(facing);
                    Block adjacentBlock = world.BlockAccessor.GetBlock(adjacentPos);

                    // Count taps
                    if (adjacentBlock is BlockSapTap)
                    {
                        tapCount++;
                    }

                    // Continue searching tree
                    if (!checkedPositions.Contains(adjacentPos) && IsValidSapSource(adjacentBlock))
                    {
                        toCheck.Enqueue(adjacentPos);
                        checkedPositions.Add(adjacentPos);
                    }
                }
            }

            return tapCount;
        }

        public override void OnBlockBroken(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1)
        {
            base.OnBlockBroken(world, pos, byPlayer, dropQuantityMultiplier);
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            BlockEntitySapTap be = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntitySapTap;
            if (be != null)
            {
                return be.OnInteract(byPlayer);
            }

            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }
    }
}
