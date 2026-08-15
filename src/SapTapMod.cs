using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.API.Client;

namespace SapTap
{
    public class SapTapMod : ModSystem
    {
        public override void Start(ICoreAPI api)
        {
            base.Start(api);
            
            api.RegisterBlockClass("BlockSapTap", typeof(BlockSapTap));
            api.RegisterBlockEntityClass("BlockEntitySapTap", typeof(BlockEntitySapTap));
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            base.StartServerSide(api);
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            base.StartClientSide(api);
        }
    }
}
