using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace SapTap
{
    /// <summary>
    /// A sap spile: a small metal tube hammered into a resin-oozing log, with a hook
    /// underneath that a wooden bucket hangs from.
    /// </summary>
    public class BlockSapTap : Block
    {
        public const int MaxTapsPerTree = 2;

        /// <summary>How far the search for other taps may spread through one tree.</summary>
        private const int TreeSearchLimit = 400;

        private WorldInteraction[] interactions;

        /// <summary>
        /// The horizontal direction from the tap towards the log it is driven into.
        /// The shape is authored for "north", so this is also what the shape rotation keys off.
        /// </summary>
        public BlockFacing AttachedSide => BlockFacing.FromCode(Variant["side"]) ?? BlockFacing.NORTH;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            if (api is ICoreClientAPI capi)
            {
                const string cacheKey = "saptapInteractions";
                if (api.ObjectCache.TryGetValue(cacheKey, out object cached))
                {
                    interactions = (WorldInteraction[])cached;
                }
                else
                {
                    List<ItemStack> buckets = new List<ItemStack>();
                    foreach (Block block in api.World.Blocks)
                    {
                        if (block?.Code != null && IsBucket(block)) buckets.Add(new ItemStack(block));
                    }

                    interactions = new WorldInteraction[]
                    {
                        new WorldInteraction
                        {
                            ActionLangCode = "saptap:blockhelp-hangbucket",
                            MouseButton = EnumMouseButton.Right,
                            Itemstacks = buckets.ToArray(),
                            GetMatchingStacks = (wi, bs, es) =>
                            {
                                BlockEntitySapTap be = capi.World.BlockAccessor.GetBlockEntity(bs.Position) as BlockEntitySapTap;
                                return be?.BucketStack == null ? wi.Itemstacks : null;
                            }
                        },
                        new WorldInteraction
                        {
                            ActionLangCode = "saptap:blockhelp-takebucket",
                            MouseButton = EnumMouseButton.Right,
                            RequireFreeHand = true,
                            ShouldApply = (wi, bs, es) =>
                            {
                                BlockEntitySapTap be = capi.World.BlockAccessor.GetBlockEntity(bs.Position) as BlockEntitySapTap;
                                return be?.BucketStack != null;
                            }
                        }
                    };

                    api.ObjectCache[cacheKey] = interactions;
                }
            }
        }

        public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
        {
            return (interactions ?? new WorldInteraction[0]).Append(base.GetPlacedBlockInteractionHelp(world, selection, forPlayer));
        }

        #region Placement

        // Placement runs through OnHeldInteractStart rather than the default block placement
        // path, because a resin log's own harvest interaction would otherwise swallow the click.
        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling)
        {
            if (blockSel == null) return;

            IPlayer byPlayer = (byEntity as EntityPlayer)?.Player;
            if (byPlayer == null) return;

            IWorldAccessor world = byEntity.World;
            BlockPos tapPos = blockSel.Position.AddCopy(blockSel.Face);

            string failureCode = null;
            if (!CanTapHere(world, blockSel, ref failureCode))
            {
                // Not a valid target: fall through to whatever the log itself does.
                if (failureCode != null)
                {
                    handHandling = EnumHandHandling.PreventDefault;
                    ShowError(world, failureCode);
                }
                return;
            }

            if (!world.Claims.TryAccess(byPlayer, tapPos, EnumBlockAccessFlags.BuildOrBreak))
            {
                handHandling = EnumHandHandling.PreventDefault;
                return;
            }

            // "side" points from the tap back towards the log.
            Block orientedBlock = world.GetBlock(CodeWithVariant("side", blockSel.Face.Opposite.Code));
            if (orientedBlock == null) return;

            world.BlockAccessor.SetBlock(orientedBlock.BlockId, tapPos);
            world.BlockAccessor.TriggerNeighbourBlockUpdate(tapPos);
            if (orientedBlock.Sounds != null)
            {
                world.PlaySoundAt(orientedBlock.Sounds.Place, tapPos, 0, byPlayer);
            }

            if (byPlayer.WorldData.CurrentGameMode != EnumGameMode.Creative)
            {
                slot.TakeOut(1);
                slot.MarkDirty();
            }

            handHandling = EnumHandHandling.PreventDefault;
        }

        public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref string failureCode)
        {
            // Reached only via paths that bypass OnHeldInteractStart. Keep the same rules so a
            // tap can never end up on a block that has no sap in it.
            if (!CanTapHere(world, blockSel, ref failureCode))
            {
                failureCode = failureCode ?? "saptap-requiresresinlog";
                return false;
            }

            return base.TryPlaceBlock(world, byPlayer, itemstack, blockSel, ref failureCode);
        }

        /// <summary>
        /// Validates a click on a log: the target must ooze resin, the space in front of it must
        /// be free, and the tree must not already carry <see cref="MaxTapsPerTree"/> taps.
        /// A null failureCode means "not our business", anything else is worth telling the player.
        /// </summary>
        private bool CanTapHere(IWorldAccessor world, BlockSelection blockSel, ref string failureCode)
        {
            if (blockSel.Face.IsVertical)
            {
                // Only the trunk sides can be tapped.
                failureCode = null;
                return false;
            }

            Block logBlock = world.BlockAccessor.GetBlock(blockSel.Position);
            if (!IsSapSource(logBlock))
            {
                failureCode = null;
                return false;
            }

            BlockPos tapPos = blockSel.Position.AddCopy(blockSel.Face);
            Block atTapPos = world.BlockAccessor.GetBlock(tapPos);
            if (atTapPos.Id != 0 && !atTapPos.IsReplacableBy(this))
            {
                failureCode = "saptap-noroom";
                return false;
            }

            if (CountTapsOnTree(world, blockSel.Position) >= MaxTapsPerTree)
            {
                failureCode = "saptap-treehasmaxtaps";
                return false;
            }

            return true;
        }

        private void ShowError(IWorldAccessor world, string failureCode)
        {
            if (world.Side != EnumAppSide.Client) return;

            string message = failureCode == "saptap-treehasmaxtaps"
                ? Lang.Get("saptap:error-treehasmaxtaps", MaxTapsPerTree)
                : Lang.Get("saptap:error-noroom");

            (world.Api as ICoreClientAPI)?.TriggerIngameError(this, failureCode, message);
        }

        #endregion

        #region Tree inspection

        /// <summary>True for a log that is currently oozing resin (vanilla: pine and acacia).</summary>
        public static bool IsSapSource(Block block)
        {
            if (block?.Code == null) return false;

            string path = block.Code.Path;
            return path.StartsWithOrdinal("log-resin-");
        }

        /// <summary>
        /// Walks the tree the given log belongs to and counts the sap taps hanging off it.
        /// Connected logs are found through the tree felling group vanilla already assigns to
        /// grown trunks, so the limit really is per tree and not per resin patch.
        /// </summary>
        public static int CountTapsOnTree(IWorldAccessor world, BlockPos logPos)
        {
            Block startBlock = world.BlockAccessor.GetBlock(logPos);
            string fellingGroup = TreeFellingGroup(startBlock);
            string wood = startBlock?.Variant?["wood"];

            HashSet<BlockPos> visited = new HashSet<BlockPos> { logPos.Copy() };
            Queue<BlockPos> queue = new Queue<BlockPos>();
            queue.Enqueue(logPos.Copy());

            HashSet<BlockPos> countedTaps = new HashSet<BlockPos>();

            while (queue.Count > 0 && visited.Count < TreeSearchLimit)
            {
                BlockPos current = queue.Dequeue();

                foreach (BlockFacing facing in BlockFacing.ALLFACES)
                {
                    BlockPos neighbourPos = current.AddCopy(facing);
                    if (visited.Contains(neighbourPos)) continue;

                    Block neighbour = world.BlockAccessor.GetBlock(neighbourPos);

                    if (neighbour is BlockSapTap)
                    {
                        countedTaps.Add(neighbourPos);
                        continue;
                    }

                    if (IsSameTree(neighbour, fellingGroup, wood))
                    {
                        visited.Add(neighbourPos);
                        queue.Enqueue(neighbourPos);
                    }
                }
            }

            return countedTaps.Count;
        }

        private static bool IsSameTree(Block block, string fellingGroup, string wood)
        {
            if (block?.Code == null || !block.Code.Path.StartsWithOrdinal("log")) return false;

            string group = TreeFellingGroup(block);
            if (fellingGroup != null && group != null) return group == fellingGroup;

            // Player-placed logs carry no felling group; fall back to matching the wood type.
            return wood != null && block.Variant?["wood"] == wood;
        }

        private static string TreeFellingGroup(Block block)
        {
            return block?.Attributes?["treeFellingGroupCode"].AsString();
        }

        public static bool IsBucket(Block block)
        {
            if (block?.Code == null) return false;
            if (!block.Code.Path.Contains("bucket")) return false;

            // Vanilla buckets (and modded ones that follow the pattern) declare their capacity here.
            return block.Attributes?["liquidContainerProps"].Exists == true;
        }

        #endregion

        #region Interaction and boxes

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            BlockEntitySapTap be = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntitySapTap;
            if (be != null && be.OnInteract(byPlayer)) return true;

            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }

        public override Cuboidf[] GetSelectionBoxes(IBlockAccessor blockAccessor, BlockPos pos)
        {
            BlockEntitySapTap be = blockAccessor.GetBlockEntity(pos) as BlockEntitySapTap;
            if (be?.BucketStack != null)
            {
                // Grow the box so the hanging bucket itself is clickable.
                return new[] { BoxFacing(AttachedSide, 0.18f, 0.82f, 0f, 0.83f, 0.01f, 0.96f) };
            }

            return new[] { BoxFacing(AttachedSide, 0.34f, 0.66f, 0f, 0.63f, 0.55f, 0.96f) };
        }

        /// <summary>
        /// Builds a box from the tap's own frame of reference, so it follows the block without
        /// depending on which way a rotation happens to turn. "out" is measured from the bark
        /// plane along the direction the tube points, "perp" is the axis parallel to the bark.
        /// </summary>
        private static Cuboidf BoxFacing(BlockFacing side, float perpMin, float perpMax, float outMin, float outMax, float yMin, float yMax)
        {
            if (side == BlockFacing.NORTH) return new Cuboidf(perpMin, yMin, outMin, perpMax, yMax, outMax);
            if (side == BlockFacing.SOUTH) return new Cuboidf(perpMin, yMin, 1 - outMax, perpMax, yMax, 1 - outMin);
            if (side == BlockFacing.WEST) return new Cuboidf(outMin, yMin, perpMin, outMax, yMax, perpMax);
            return new Cuboidf(1 - outMax, yMin, perpMin, 1 - outMin, yMax, perpMax);
        }

        public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
        {
            Block northVariant = world.GetBlock(CodeWithVariant("side", "north"));
            return new ItemStack(northVariant ?? this);
        }

        #endregion
    }
}
