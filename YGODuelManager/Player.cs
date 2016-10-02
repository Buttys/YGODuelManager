using System.IO;
using YGOSharp.Network;
using YGOSharp.Network.Enums;

namespace YGODuelManager
{
    public class Player
    {
        public Game Game { get; private set; }
        public string Name { get;  set; }
        public bool IsAuthentified { get; private set; }
        public int Type { get; set; }
        public PlayerState State { get; set; }
        private YGOClient _client;

        public Player(YGOClient client)
        {
            Type = (int)PlayerType.Undefined;
            State = PlayerState.None;
            _client = client;
        }

        public bool Update()
        {
            _client.Update();

            return _client.IsConnected;
        }

        public void Send(BinaryWriter packet)
        {
            _client.Send(packet);
        }

        private void Send(byte[] packet)
        {
            _client.Send(packet);
        }

        public void Disconnect()
        {
            _client.Close();

        }

        public void OnDisconnected()
        {
            if (IsAuthentified & Game != null)
                Game.LeaveGame(this);
        }

        public bool Equals(Player player)
        {
            return ReferenceEquals(this, player);
        }

        public void Parse(BinaryReader packet)
        {
            //handle packets here


            //Allow Addon manager to handle these packets
            AddonsManager.NewPacket(packet, this);
        }
    }
}