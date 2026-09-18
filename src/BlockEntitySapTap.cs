using System;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace SapTap
{
    public class BlockEntitySapTap : BlockEntity
    {
        /// <summary>Matches the capacity of a vanilla wooden bucket.</summary>
        public const int MaxSapLiters = 10;

        private const double SapPerGameHour = 0.5;
        private const int TickIntervalMs = 10000;

        /// <summary>
        /// How far the hanging bucket is lifted so its bail settles onto the hook rod, which the
        /// tap shape puts at y=10.5. The bucket's own bail sits at y=10.0, so it rides 0.3 units
        /// into the rod - just enough that the two never end up as coplanar faces.
        /// </summary>
        private const float BucketLift = 0.3f / 16f;

        private static readonly object meshCacheLock = new object();

        private ItemStack bucketStack;
        public ItemStack BucketStack => bucketStack;

        private double currentSapLiters;
        private double lastUpdateTotalHours;
        private int lastSyncedLiters;

        private BlockSapTap ownBlock;

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            ownBlock = Block as BlockSapTap;

            if (api.Side == EnumAppSide.Server)
            {
                RegisterGameTickListener(OnServerTick, TickIntervalMs);
            }
        }

        #region Sap collection

        private void OnServerTick(float dt)
        {
            if (bucketStack == null || currentSapLiters >= MaxSapLiters) return;

            double currentTotalHours = Api.World.Calendar.TotalHours;
            double hoursPassed = currentTotalHours - lastUpdateTotalHours;
            lastUpdateTotalHours = currentTotalHours;

            if (hoursPassed <= 0) return;

            currentSapLiters = Math.Min(currentSapLiters + hoursPassed * CollectionRate(), MaxSapLiters);

            // Only sync on a whole-litre change. Syncing every tick would make every client
            // re-tesselate the chunk around each tap for a number they cannot see change.
            int liters = (int)currentSapLiters;
            if (liters != lastSyncedLiters)
            {
                lastSyncedLiters = liters;
                MarkDirty();
            }
        }

        private double CollectionRate()
        {
            return SapPerGameHour * SeasonalModifier();
        }

        /// <summary>
        /// Sap runs best in spring and barely at all in winter. Ask the calendar which season it
        /// is rather than slicing GetSeasonRel by hand - the game's own cut points are uneven
        /// (winter wraps around 0, spring only starts at 0.2164) and it accounts for hemisphere.
        /// </summary>
        public double SeasonalModifier()
        {
            switch (Api.World.Calendar.GetSeason(Pos))
            {
                case EnumSeason.Spring: return 1.5;
                case EnumSeason.Summer: return 1.0;
                case EnumSeason.Fall: return 0.5;
                default: return 0.25; // winter
            }
        }

        #endregion

        #region Interaction

        public bool OnInteract(IPlayer byPlayer)
        {
            ItemSlot activeSlot = byPlayer.InventoryManager.ActiveHotbarSlot;

            if (bucketStack == null) return TryHangBucket(byPlayer, activeSlot);
            if (activeSlot.Empty) return TakeBucket(byPlayer, activeSlot);

            return false;
        }

        private bool TryHangBucket(IPlayer byPlayer, ItemSlot activeSlot)
        {
            ItemStack heldStack = activeSlot?.Itemstack;
            if (heldStack == null) return false;

            if (!BlockSapTap.IsBucket(heldStack.Block))
            {
                ShowError(byPlayer, "saptap:error-notabucket");
                return true;
            }

            if (!IsEmptyContainer(heldStack))
            {
                ShowError(byPlayer, "saptap:error-bucketnotempty");
                return true;
            }

            bucketStack = heldStack.Clone();
            bucketStack.StackSize = 1;
            activeSlot.TakeOut(1);
            activeSlot.MarkDirty();

            currentSapLiters = 0;
            lastSyncedLiters = 0;
            lastUpdateTotalHours = Api.World.Calendar.TotalHours;

            PlayBucketSound();
            MarkDirty(true);
            return true;
        }

        private bool TakeBucket(IPlayer byPlayer, ItemSlot activeSlot)
        {
            int resinCount = (int)currentSapLiters;
            double collectedLiters = currentSapLiters;

            // The bucket goes back into the hand FIRST. The hand has to be empty to get here, so
            // handing out the resin beforehand would drop it into this very slot and the bucket
            // would then overwrite it. Done on both sides so the client predicts the swap.
            activeSlot.Itemstack = bucketStack.Clone();
            activeSlot.MarkDirty();

            bucketStack = null;
            currentSapLiters = 0;
            lastSyncedLiters = 0;
            lastUpdateTotalHours = 0;

            // Handing out items is authoritative, so it only happens server side.
            if (Api.Side == EnumAppSide.Server)
            {
                if (resinCount > 0) GiveResin(byPlayer, resinCount);

                string message = resinCount >= MaxSapLiters
                    ? Lang.Get("saptap:message-collected", resinCount)
                    : Lang.Get("saptap:message-notfullyet", collectedLiters, MaxSapLiters);
                (byPlayer as IServerPlayer)?.SendMessage(GlobalConstants.InfoLogChatGroup, message, EnumChatType.Notification);
            }

            PlayBucketSound();
            MarkDirty(true);
            return true;
        }

        /// <summary>Hands the collected resin over, dropping it at the tap if there is no room.</summary>
        private void GiveResin(IPlayer byPlayer, int quantity)
        {
            Item resin = Api.World.GetItem(new AssetLocation("game:resin"));
            if (resin == null)
            {
                Api.Logger.Warning("[SapTap] game:resin not found, nothing to hand out.");
                return;
            }

            ItemStack resinStack = new ItemStack(resin, quantity);
            if (!byPlayer.InventoryManager.TryGiveItemstack(resinStack))
            {
                Api.World.SpawnItemEntity(resinStack, Pos.ToVec3d().Add(0.5, 0.5, 0.5));
            }
        }

        /// <summary>A liquid container stores what is in it under the "contents" tree.</summary>
        private static bool IsEmptyContainer(ItemStack stack)
        {
            ITreeAttribute contents = stack.Attributes?.GetTreeAttribute("contents");
            return contents == null || contents.Count == 0;
        }

        private void ShowError(IPlayer byPlayer, string langKey)
        {
            if (Api.Side == EnumAppSide.Client)
            {
                (Api as ICoreClientAPI)?.TriggerIngameError(this, langKey, Lang.Get(langKey));
            }
        }

        private void PlayBucketSound()
        {
            Api.World.PlaySoundAt(new AssetLocation("game:sounds/block/planks"), Pos, 0, null, true, 12);
        }

        public override void OnBlockBroken(IPlayer byPlayer = null)
        {
            base.OnBlockBroken(byPlayer);

            if (bucketStack != null && Api.Side == EnumAppSide.Server)
            {
                Api.World.SpawnItemEntity(bucketStack, Pos.ToVec3d().Add(0.5, 0.5, 0.5));

                int resinCount = (int)currentSapLiters;
                Item resin = resinCount > 0 ? Api.World.GetItem(new AssetLocation("game:resin")) : null;
                if (resin != null)
                {
                    Api.World.SpawnItemEntity(new ItemStack(resin, resinCount), Pos.ToVec3d().Add(0.5, 0.5, 0.5));
                }

                bucketStack = null;
            }
        }

        #endregion

        #region Rendering

        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator)
        {
            // The tap itself is a plain block shape; only the hanging bucket is added here, so
            // that it is the real vanilla bucket mesh rather than a lookalike baked into the tap.
            ItemStack stack = bucketStack;
            if (stack?.Block == null) return false;

            MeshData mesh = BucketMesh(stack.Block, tessThreadTesselator);
            if (mesh != null) mesher.AddMeshData(mesh);

            return false;
        }

        private MeshData BucketMesh(Block bucketBlock, ITesselatorAPI tesselator)
        {
            ICoreClientAPI capi = Api as ICoreClientAPI;
            if (capi == null) return null;

            // The bucket's rope bail runs along X, so it drops straight onto the hook of a tap
            // that points along Z. For a tap pointing along X the bucket needs a quarter turn;
            // 90 and 270 look identical here, so no rotation direction has to be assumed.
            bool quarterTurn = ownBlock != null && ownBlock.AttachedSide.Axis == EnumAxis.X;

            string cacheKey = "saptapBucketMesh-" + bucketBlock.Code + (quarterTurn ? "-x" : "-z");

            lock (meshCacheLock)
            {
                if (capi.ObjectCache.TryGetValue(cacheKey, out object cached)) return (MeshData)cached;

                MeshData mesh;
                tesselator.TesselateBlock(bucketBlock, out mesh);
                if (mesh == null) return null;

                if (quarterTurn) mesh.Rotate(new Vec3f(0.5f, 0.5f, 0.5f), 0, GameMath.PIHALF, 0);
                mesh.Translate(0, BucketLift, 0);

                capi.ObjectCache[cacheKey] = mesh;
                return mesh;
            }
        }

        #endregion

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            base.GetBlockInfo(forPlayer, dsc);

            if (bucketStack == null)
            {
                dsc.AppendLine(Lang.Get("saptap:blockinfo-nobucket"));
            }
            else if (currentSapLiters >= MaxSapLiters)
            {
                dsc.AppendLine(Lang.Get("saptap:blockinfo-full", MaxSapLiters));
            }
            else
            {
                dsc.AppendLine(Lang.Get("saptap:blockinfo-collecting", currentSapLiters, MaxSapLiters));
            }

            dsc.AppendLine(Lang.Get("saptap:blockinfo-season", SeasonalModifier()));
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);

            if (bucketStack != null) tree.SetItemstack("bucketStack", bucketStack);
            tree.SetDouble("currentSapLiters", currentSapLiters);
            tree.SetDouble("lastUpdateTotalHours", lastUpdateTotalHours);
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);

            ItemStack previousBucket = bucketStack;

            bucketStack = tree.GetItemstack("bucketStack");
            bucketStack?.ResolveBlockOrItem(worldAccessForResolve);

            currentSapLiters = tree.GetDouble("currentSapLiters");
            lastUpdateTotalHours = tree.GetDouble("lastUpdateTotalHours");
            lastSyncedLiters = (int)currentSapLiters;

            // Appearing or disappearing is the only change the mesh cares about.
            if (Api?.Side == EnumAppSide.Client && (previousBucket == null) != (bucketStack == null))
            {
                MarkDirty(true);
            }
        }
    }
}
