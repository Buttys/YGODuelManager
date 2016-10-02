using System.IO;
using YGODuelManager.Events;

namespace YGODuelManager.Addons
{
    public class PacketHandler : AddonBase
    {
        public PacketHandler(CoreServer server)
            :base(server)
        {
            AddonsManager.OnPlayerPacket += HandlePacket;
        }

        void HandlePacket(BinaryReader reader, PlayerEventArgs e)
        {
            //handle packets here
        }
    }
}
