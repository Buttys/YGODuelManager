using System;
using System.Collections.Generic;
using System.IO;
using YGODuelManager.Addons;
using YGODuelManager.Events;

namespace YGODuelManager
{
    public class AddonsManager
    {
        public List<AddonBase> Addons { get; private set; }

        public static event Action<object, EventArgs> OnRoomCreate;
        public static event Action<object, EventArgs> OnPlayerJoin;
        public static event Action<object, EventArgs> OnPlayerLeave;
        public static event Action<object, EventArgs> OnRoomClose;
        public static event Action<object, EventArgs> OnRoomStart;

        public static event Action<BinaryReader, PlayerEventArgs> OnPlayerPacket;

        public AddonsManager(CoreServer server)
        {
            Addons = new List<AddonBase>();
            Addons.Add(new PacketHandler(server));
        }

        public void Update()
        {
            foreach (AddonBase addon in Addons)
                addon.Update();
        }

        public static void NewPacket(BinaryReader reader, Player player)
        {
            OnPlayerPacket(reader, new PlayerEventArgs(player));
        }

        public static void RoomCreated(Game game)
        {
            OnRoomCreate?.Invoke(game, EventArgs.Empty);
        }

        public static void RoomStart(Game game)
        {
            OnRoomStart?.Invoke(game, EventArgs.Empty);
        }

        public static void RoomJoin(Game game)
        {
            OnPlayerJoin?.Invoke(game, EventArgs.Empty);
        }

        public static void RoomLeft(Game game)
        {
            OnPlayerLeave?.Invoke(game, EventArgs.Empty);
        }
        public static void RoomClose(Game game)
        {
            OnRoomClose?.Invoke(game, EventArgs.Empty);
        }
    }
}
