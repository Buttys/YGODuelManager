using System;
using System.IO;
using System.Net;
using YGOSharp.Network;
using YGOSharp.Network.Enums;
using YGOSharp.Network.Utils;

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
        private YGOClient _coreConnection;
        private BinaryWriter _authMessage;
        private BinaryWriter _joinMessage;

        public Player(YGOClient client)
        {
            Type = (int)PlayerType.Undefined;
            State = PlayerState.None;
            _client = client;
            _coreConnection = new YGOClient();
        }

        public bool Update()
        {
            _client.Update();
            if (IsAuthentified)
                _coreConnection.Update();

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
            if (_coreConnection != null)
                _coreConnection.Close();
        }

        public bool Equals(Player player)
        {
            return ReferenceEquals(this, player);
        }

        public void Parse(BinaryReader packet)
        {
            CtosMessage msg = (CtosMessage)packet.ReadByte();

            //Log.ServerLog("Server >> " + msg.ToString());

            switch (msg)
            {
                case CtosMessage.PlayerInfo:
                    OnPlayerInfo(packet);
                    break;
                case CtosMessage.JoinGame:
                    OnJoinGame(packet);
                    break;
                case CtosMessage.CreateGame:
                    OnCreateGame(packet);
                    break;
            }
            if (!IsAuthentified || Game == null)
                return;

            //handle other messages below

            switch(msg)
            {
                default:
                    break;
            }

            //otherwise send them to the core!
            if (_coreConnection.IsConnected)
            {
                packet.BaseStream.Position = 0;
                _coreConnection.Send(packet.ReadToEnd());
            }
        }

        private void OnPlayerInfo(BinaryReader packet)
        {
            string playerName = packet.ReadUnicode(Config.GetInt("ExpandedName",20));

            if (playerName.Length < 1)
            {
                SendRoomMessage(Config.GetString("InvalidUsername","[Invalid Username]"));
                return;
            }

            Name = playerName.Split('$')[0];

            BinaryWriter playerInfo = GamePacketFactory.Create(CtosMessage.PlayerInfo);
            playerInfo.WriteUnicode(Name, 20);

            packet.BaseStream.Position = 0;
            _authMessage = playerInfo;
        }

        private void OnCreateGame(BinaryReader packet)
        {
            GameInfo info = new GameInfo();
            info.Banlist = packet.ReadUInt32(); //banlist
            info.Rule = packet.ReadByte();//rule
            info.Mode = packet.ReadByte();//mode
            info.EnablePriority = packet.ReadByte() > 0;//priority
            info.NoCheckDeck = packet.ReadByte() > 0;//nocheckdeck
            info.NoShuffleDeck = packet.ReadByte() > 0;//no shuffle
            //C++ padding: 5 bytes + 3 bytes = 8 bytes
            for (int i = 0; i < 3; i++)
                packet.ReadByte();
            info.Lifepoints = packet.ReadInt32();//lifepoints
            info.StartHand = packet.ReadByte();//handsize
            info.DrawCount = packet.ReadByte();//draw count
            info.Timer = packet.ReadInt16();//timer
            info.GameName = packet.ReadUnicode(20);//hostname
            info.Password = packet.ReadUnicode(30); //password

            if (info.GameName.Length < 1)
            {
                SendRoomMessage(Config.GetString("InvalidRoom", "[Invalid RoomName]"));
                return;
            }

            packet.BaseStream.Position = 0;
            _joinMessage = new BinaryWriter(new MemoryStream());
            _joinMessage.Write(packet.ReadToEnd());

            GameManager.CreateRoom(this, info);
        }

        private void OnJoinGame(BinaryReader packet)
        {
            if (Name == null || Type != (int)PlayerType.Undefined)
                return;

            int version = packet.ReadInt16();
            if(version != Config.GetUInt("ClientVersion",Program.ClientVersion))
            {
                SendRoomMessage(Config.GetString("InvalidVersion","[Invalid Version]"));
                return;
            }

            packet.ReadInt32();//gameid
            packet.ReadInt16();
            string roomName = packet.ReadUnicode(Config.GetInt("ExpandedRoomName",40));

            if(roomName.Length < 1)
            {
                SendRoomMessage(Config.GetString("InvalidRoom","[Invalid RoomName]"));
                return;
            }


            packet.BaseStream.Position = 0;
            _joinMessage = new BinaryWriter(new MemoryStream());
            _joinMessage.Write(packet.ReadToEnd());

            Game = GameManager.RequestRoom(this, new GameInfo() { GameName = roomName });

            if(Game != null)
                CoreConnect(Game.Port);   
        }

        public void JoinGame(Game game)
        {
            Game = game;
            game.JoinGame(this);
            //create join info //allows editing of player name before sending

            _authMessage = GamePacketFactory.Create(CtosMessage.PlayerInfo);
            _authMessage.WriteUnicode(Name, 20);


            if (Equals(game.Host))
            {
                //required to change duel settings if preloading cores insted of loading them on demand
                _joinMessage = GamePacketFactory.Create(CtosMessage.CreateGame);
                _joinMessage.Write(Game.Info.Banlist);
                _joinMessage.Write((byte)Game.Info.Rule);
                _joinMessage.Write((byte)Game.Info.Mode);
                _joinMessage.Write(Game.Info.EnablePriority);
                _joinMessage.Write(Game.Info.NoCheckDeck);
                _joinMessage.Write(Game.Info.NoShuffleDeck);
                //C++ padding: 5 bytes + 3 bytes = 8 bytes
                for (int i = 0; i < 3; i++)
                    _joinMessage.Write((byte)0);
                _joinMessage.Write(Game.Info.Lifepoints);
                _joinMessage.Write((byte)Game.Info.StartHand);
                _joinMessage.Write((byte)Game.Info.DrawCount);
                _joinMessage.Write((short)Game.Info.Timer + 1 * Config.GetInt("DEFAULT_TIMER", 240));
                _joinMessage.WriteUnicode(Game.Info.GameName, 20);
                _joinMessage.WriteUnicode(Game.Info.Password, 30);
            }

            CoreConnect(game.Port);
        }

        public void SendRoomMessage(string message)
        {
            BinaryWriter joinPacket = GamePacketFactory.Create(StocMessage.JoinGame);

            joinPacket.Write((byte)0x1B);
            joinPacket.Write((byte)0x0C);
            joinPacket.Write((byte)0xE9);
            joinPacket.Write((byte)0x25);

            joinPacket.Write((byte)2);
            joinPacket.Write((byte)0);

            joinPacket.Write((byte)0);
            joinPacket.Write((byte)0);
            joinPacket.Write((byte)0);

            joinPacket.Write(8000);
            joinPacket.Write((byte)5);
            joinPacket.Write((byte)1);
            joinPacket.Write((short)0);

            Send(joinPacket);

            BinaryWriter changePacket = GamePacketFactory.Create(StocMessage.TypeChange);

            changePacket.Write((byte)0);

            Send(changePacket);

            BinaryWriter enterpacket = GamePacketFactory.Create(StocMessage.HsPlayerEnter);

            enterpacket.WriteUnicode(message, 20);
            enterpacket.Write((byte)0);
            //padding
            enterpacket.Write((byte)0);

            Send(enterpacket);
        }
        private void CoreConnect(int port)
        {
            try
            {
                _coreConnection.PacketReceived += packet => ParseCore(packet);
                _coreConnection.Disconnected += packet => CoreDisconnected();
                _coreConnection.Connect(IPAddress.Loopback, port);
                _coreConnection.Send(_authMessage);
                _coreConnection.Send(_joinMessage);
#if DEBUG
                SendServerMessage("Welcome to Buttys test server, Duel data will not be recorded.");
#endif
                IsAuthentified = true;
            }
            catch(Exception)
            {
                SendRoomMessage(Config.GetString("CoreConnectionFailed","[Core Error]"));
            }
        }
        private void ParseCore(BinaryReader packet)
        {
            StocMessage msg = (StocMessage)packet.ReadByte();

            switch(msg)
            {
                case StocMessage.TypeChange:
                    Game?.PlayerTypeChange(this, packet);
                    break;
                case StocMessage.DuelStart:
                    Game?.UpdateGameState(this, GameState.Starting);
                    break;
                case StocMessage.SelectTp:
                    Game?.UpdateGameState(this, GameState.Duel);
                    break;
                case StocMessage.ChangeSide:
                    Game?.UpdateGameState(this, GameState.Side);
                    break;
            }


            //Console.WriteLine("Core << " + msg);
            packet.BaseStream.Position = 0;
            Send(packet.ReadToEnd());
        }

        public void SendServerMessage(string message)
        {
            BinaryWriter packet = GamePacketFactory.Create(StocMessage.Chat);
            packet.Write((short)PlayerType.Pink);
            packet.WriteUnicode(message, message.Length + 1);
            Send(packet);
        }

        private void CoreDisconnected()
        {
            //tell the user the core crashed/disconnected
            //send end game packet?
            if (_client.IsConnected)
                _client.Close();
        }

    }
}