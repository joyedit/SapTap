using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace SapTap
{
    public class BlockEntitySapTap : BlockEntity
    {
        private const int MaxSapLiters = 10;
        private const double SapPerGameHour = 0.5; // ~20 game hours to fill bucket
        
        private ItemStack bucketStack;
        public ItemStack BucketStack => bucketStack;
        
        private double currentSapLiters = 0;
        private double lastUpdateTotalHours = 0;
        
        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            
            if (api.Side == EnumAppSide.Server)
            {
                RegisterGameTickListener(OnServerTick, 5000); // Check every 5 seconds
            }
            // TODO: Add bucket renderer later when we figure out the API
        }

        private void OnServerTick(float dt)
        {
            if (bucketStack == null) return;

            // Calculate time passed
            double currentTotalHours = Api.World.Calendar.TotalHours;
            if (lastUpdateTotalHours == 0)
            {
                lastUpdateTotalHours = currentTotalHours;
                return;
            }

            double hoursPassed = currentTotalHours - lastUpdateTotalHours;
            
            // Calculate sap collection rate (affected by season and tree health)
            double collectionRate = CalculateCollectionRate();
            double sapGained = hoursPassed * collectionRate;

            currentSapLiters = Math.Min(currentSapLiters + sapGained, MaxSapLiters);
            lastUpdateTotalHours = currentTotalHours;

            MarkDirty(true);
        }

        private double CalculateCollectionRate()
        {
            double baseRate = SapPerGameHour;
            
            // Seasonal modifier
            double seasonMod = GetSeasonalModifier();
            
            // Tree health modifier (to be implemented)
            double healthMod = 1.0;

            return baseRate * seasonMod * healthMod;
        }

        private double GetSeasonalModifier()
        {
            // Spring = best collection (1.5x)
            // Summer = good collection (1.0x)
            // Fall = reduced collection (0.5x)
            // Winter = minimal collection (0.25x)
            
            double season = Api.World.Calendar.GetSeasonRel(Pos);
            
            if (season < 0.25) // Spring
                return 1.5;
            else if (season < 0.5) // Summer
                return 1.0;
            else if (season < 0.75) // Fall
                return 0.5;
            else // Winter
                return 0.25;
        }

        public bool OnInteract(IPlayer byPlayer)
        {
            ItemSlot activeSlot = byPlayer.InventoryManager.ActiveHotbarSlot;

            // Attaching a bucket
            if (bucketStack == null && activeSlot?.Itemstack != null)
            {
                // Check if it's a bucket (simple check by item code)
                string itemCode = activeSlot.Itemstack.Collectible.Code.Path;
                if (itemCode.Contains("bucket") && !itemCode.Contains("wooden"))
                {
                    bucketStack = activeSlot.Itemstack.Clone();
                    bucketStack.StackSize = 1;
                    activeSlot.TakeOut(1);
                    activeSlot.MarkDirty();
                    
                    currentSapLiters = 0;
                    lastUpdateTotalHours = Api.World.Calendar.TotalHours;
                    
                    // Switch to bucket variant
                    SwitchToBucketVariant(true);
                    
                    MarkDirty(true);
                    return true;
                }
            }
            // Retrieving a bucket
            else if (bucketStack != null && activeSlot.Empty)
            {
                // Fill the bucket with sap if enough has collected
                if (currentSapLiters >= MaxSapLiters)
                {
                    // Try to get the resin item - it's just called "resin" in the game
                    Item resinItem = Api.World.GetItem(new AssetLocation("game:resin"));
                    
                    if (resinItem != null)
                    {
                        // Create a stack of resin (10 liters = 10 resin)
                        ItemStack resinStack = new ItemStack(resinItem, (int)currentSapLiters);
                        
                        // Try to give the player the resin
                        if (byPlayer.InventoryManager.TryGiveItemstack(resinStack))
                        {
                            // Successfully gave resin, now return the bucket to active slot
                            activeSlot.Itemstack = bucketStack.Clone();
                            activeSlot.MarkDirty();
                        }
                        else
                        {
                            // Inventory full, just give back bucket
                            activeSlot.Itemstack = bucketStack.Clone();
                            activeSlot.MarkDirty();
                            
                            if (Api.Side == EnumAppSide.Server)
                            {
                                (byPlayer as IServerPlayer)?.SendMessage(0, 
                                    "Inventory full! Resin lost.", 
                                    EnumChatType.Notification);
                            }
                        }
                    }
                    else
                    {
                        // Fallback: just return the bucket if resin item doesn't exist
                        if (Api.Side == EnumAppSide.Server)
                        {
                            Api.Logger.Warning("[SapTap] Could not find resin item!");
                        }
                        activeSlot.Itemstack = bucketStack.Clone();
                        activeSlot.MarkDirty();
                    }
                    
                    bucketStack = null;
                    currentSapLiters = 0;
                    lastUpdateTotalHours = 0;
                    
                    // Switch back to empty variant
                    SwitchToBucketVariant(false);
                    
                    MarkDirty(true);
                    
                    if (Api.Side == EnumAppSide.Server)
                    {
                        (byPlayer as IServerPlayer)?.SendMessage(0, 
                            "Collected 10 liters of resin!", 
                            EnumChatType.Notification);
                    }
                    return true;
                }
                else
                {
                    // Return empty bucket if not full
                    activeSlot.Itemstack = bucketStack.Clone();
                    activeSlot.MarkDirty();
                    
                    bucketStack = null;
                    currentSapLiters = 0;
                    lastUpdateTotalHours = 0;
                    
                    // Switch back to empty variant
                    SwitchToBucketVariant(false);
                    
                    MarkDirty(true);
                    
                    if (Api.Side == EnumAppSide.Server)
                    {
                        (byPlayer as IServerPlayer)?.SendMessage(0, 
                            $"Bucket not full yet. {currentSapLiters:F1}/{MaxSapLiters} liters collected.", 
                            EnumChatType.Notification);
                    }
                    return true;
                }
            }

            return false;
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            
            if (bucketStack != null)
            {
                tree.SetItemstack("bucketStack", bucketStack);
            }
            
            tree.SetDouble("currentSapLiters", currentSapLiters);
            tree.SetDouble("lastUpdateTotalHours", lastUpdateTotalHours);
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);
            
            bucketStack = tree.GetItemstack("bucketStack");
            if (bucketStack != null)
            {
                bucketStack.ResolveBlockOrItem(worldAccessForResolve);
            }
            
            currentSapLiters = tree.GetDouble("currentSapLiters");
            lastUpdateTotalHours = tree.GetDouble("lastUpdateTotalHours");
        }
        
        private void SwitchToBucketVariant(bool hasBucket)
        {
            Block currentBlock = Api.World.BlockAccessor.GetBlock(Pos);
            if (currentBlock == null) return;
            
            string newState = hasBucket ? "bucket" : "empty";
            Block newBlock = Api.World.GetBlock(currentBlock.CodeWithVariant("state", newState));
            
            if (newBlock != null && newBlock.Id != currentBlock.Id)
            {
                Api.World.BlockAccessor.ExchangeBlock(newBlock.Id, Pos);
            }
        }
    }
}
