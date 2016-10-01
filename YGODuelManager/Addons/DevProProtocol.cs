using ServiceStack.Text;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using YGOSharp.Network.Enums;
using static YGODuelManager.Addons.DevProProtocol;

namespace YGODuelManager.Addons
{
    public class DevProProtocol : AddonBase
    {
        public enum HubClientPackets
        {
            LoginAcccepted = 0,
            LoginRefused = 1,
            KillRoom = 2,
            DisconnectUser = 4,
            GracefulShutdown = 5,
            GracefulRestart = 6,
            Shutdown = 7,
            Restart = 8,
            KillAll = 9,
            KillCrashed = 10,
            KillLazy = 11,
            MaintenanceRate = 12,
            ChangeCoreName = 13,
        }

        public enum HubServerPackets
        {
            Login = 0,
            CreateRoom = 1,
            RemoveRoom = 2,
            StartRoom = 3,
            UpdateRoomPlayers = 4,
            ShuttingDown = 5,
        }

        private readonly TcpClient m_client;
        private BinaryReader m_reader;
        private readonly Queue<byte[]> m_sendQueue;
        private string ServerName;
        private Dictionary<string, RoomInfos> m_roomdata;

        public bool IsConnected { get; private set; }
        /// <summary>
        /// Look at all this horrible devpro code
        /// i guess this should serve as a bad example for the addon API lol
        /// Copy/Pasteing ftw
        /// </summary>
        /// <param name="server"></param>
        public DevProProtocol(CoreServer server)
            :base(server)
        {
            m_client = new TcpClient();
            m_sendQueue = new Queue<byte[]>();
            m_roomdata = new Dictionary<string, RoomInfos>();
            Random rand = new Random();
            Config.Load(new string[] { "ExpandedRoomName=80", "ExpandedName=40" });
            ServerName = Config.GetString("DevServerName", "DevServer" + rand.Next(9999));
            if (Connect(Config.GetString("DevHubServer", "127.0.0.1"), Config.GetInt("DevHubPort", 6666)))
                Login();

            GameManager.OnRoomCreate += OnRoomCreated;
            GameManager.OnRoomClose += OnRoomRemoved;
            GameManager.OnPlayerJoin += OnRoomPlayersUpdate;
            GameManager.OnPlayerLeave += OnRoomPlayersUpdate;
            GameManager.OnRoomStart += OnRoomStarted;
        }

        private bool Connect(string address, int port)
        {
            try
            {
                m_client.Connect(address, port);
                m_reader = new BinaryReader(m_client.GetStream());
                IsConnected = true;
                return true;
            }
            catch (Exception)
            {
                Log.Write(typeof(DevProProtocol).Name, "Failed to connect to hub server");
                return false;
            }
        }

        public void Login()
        {
            SendPacket(HubServerPackets.Login,
                Encoding.UTF8.GetBytes(
                JsonSerializer.SerializeToString(new LoginRequest
                {
                    Username = ServerName,
                    Password = Config.GetString("DevPassword", "\"NotHere\""),
                    UID = Config.GetInt("DevDuelPort", 7911).ToString()
                })));
            Log.Write(typeof(DevProProtocol).Name, "Login request sent for port " + Config.GetInt("DevDuelPort", 7911));
        }

        private static bool IsOneByte(HubClientPackets packet)
        {
            switch (packet)
            {
                case HubClientPackets.LoginAcccepted:
                case HubClientPackets.LoginRefused:
                case HubClientPackets.GracefulShutdown:
                case HubClientPackets.GracefulRestart:
                case HubClientPackets.Shutdown:
                case HubClientPackets.Restart:
                case HubClientPackets.KillAll:
                case HubClientPackets.KillCrashed:
                    return true;
                default:
                    return false;
            }
        }

        public void SendPacket(HubServerPackets type)
        {
            SendPacket(new[] { (byte)type });
        }

        public void SendPacket(HubServerPackets type, byte[] data)
        {
            MemoryStream stream = new MemoryStream();
            BinaryWriter writer = new BinaryWriter(stream);
            writer.Write((byte)type);
            writer.Write((short)data.Length);
            writer.Write(data);
            SendPacket(stream.ToArray());
        }

        private void SendPacket(byte[] packet)
        {
            if (!IsConnected)
                return;

            lock (m_sendQueue)
                m_sendQueue.Enqueue(packet);

        }

        private void OnCommand(MessageReceived e)
        {
            switch (e.Packet)
            {
                case HubClientPackets.LoginAcccepted:
                    Log.Write(typeof(DevProProtocol).Name, "Login Accepted");
                    break;
                case HubClientPackets.LoginRefused:
                    Log.Write(typeof(DevProProtocol).Name, "Login Refused");
                    break;
                case HubClientPackets.KillRoom:
                case HubClientPackets.DisconnectUser:
                    break;
                case HubClientPackets.GracefulShutdown:
                    //ToDo
                    break;
                case HubClientPackets.GracefulRestart:
                    //ToDo
                    break;
                case HubClientPackets.MaintenanceRate:
                    //ToDo
                    break;
                case HubClientPackets.Shutdown:
                    //ToDo
                    break;
                case HubClientPackets.Restart:
                    //ToDo
                    break;
                case HubClientPackets.KillAll:
                    //ToDo
                    break;
                case HubClientPackets.KillCrashed:
                    //ToDo
                    break;
                case HubClientPackets.KillLazy:
                    //Todo
                    break;
                case HubClientPackets.ChangeCoreName:
                    //no idea what this is
                    break;
                default:
                    Log.Write(typeof(DevProProtocol).Name, "Unknown packet: " + e.Packet);
                    return;
            }
        }

        private int m_disconnectTick = 100;
        public override void Update()
        {

            if (!IsConnected)
                return;
            try
            {
                if (--m_disconnectTick <= 0)
                {
                    m_disconnectTick = 100;
                    if (CheckDisconnected())
                    {
                        Disconnect();
                        return;
                    }
                }
                //handle incoming
                while (m_client.Available >= 1)
                {
                    var packet = (HubClientPackets)m_reader.ReadByte();
                    int len = 0;
                    byte[] content = null;
                    if (!IsOneByte(packet))
                    {
                        len = m_reader.ReadInt16();
                        content = m_reader.ReadBytes(len);
                    }

                    if (len > 0)
                    {
                        if (content != null)
                        {
                            var reader = new BinaryReader(new MemoryStream(content));
                            OnCommand(new MessageReceived(packet, content, reader));
                        }
                    }
                    else
                        OnCommand(new MessageReceived(packet, null, null));
                }
                //send packet
                while (m_sendQueue.Count > 0)
                {
                    byte[] packet;
                    lock (m_sendQueue)
                        packet = m_sendQueue.Dequeue();
                    m_client.Client.BeginSend(packet, 0, packet.Length, 0, OnSend, m_client);
                }
            }
            catch (Exception)
            {
                Disconnect();
            }
        }

        private void OnRoomCreated(object sender, EventArgs e)
        {
            Game game = (Game)sender;
            RoomInfos room = RoomInfos.FromName(((Game)sender).Info.GameName,ServerName);
            room.playerList = game.PlayerList();
            for (int i = 0; i < room.eloList.Length; i++)
                room.eloList[i] = 1337;
            GameInfo gameInfo = new GameInfo()
            {
                Mode = room.mode,
                Rule = room.rule,
                Banlist = (uint)room.banListType,
                DrawCount = room.drawCount,
                StartHand = room.startHand,
                Lifepoints = room.startLp,
                EnablePriority = room.enablePriority,
                NoCheckDeck = room.isNoCheckDeck,
                NoShuffleDeck = room.isNoShuffleDeck,
                Timer = room.timer,
                GameName = room.roomName,
                Password = string.Empty
            };
            game.UpdateGameInfo(gameInfo);
            //createroom info
            if (!m_roomdata.ContainsKey(room.roomName))
                m_roomdata.Add(game.Info.GameName, room);

            SendPacket(HubServerPackets.CreateRoom, Encoding.UTF8.GetBytes(JsonSerializer.SerializeToString(room)));
        }

        private void OnRoomPlayersUpdate(object sender, EventArgs e)
        {
            Game game = (Game)sender;
            RoomInfos room = null;

            if (m_roomdata.ContainsKey(game.Info.GameName))
            {
                room = m_roomdata[game.Info.GameName];
                room.playerList = game.PlayerList();
                
            }
            if (room == null || game.State != GameState.Lobby)
                return;
            SendPacket(HubServerPackets.UpdateRoomPlayers,
                Encoding.UTF8.GetBytes(JsonSerializer.SerializeToString(
                new PacketCommand() { Command = room.GetRoomName(), Data = string.Join(",", room.playerList) + "|" + string.Join(",", room.eloList) })));
        }

        private void OnRoomStarted(object sender, EventArgs e)
        {
            Game game = (Game)sender;
            RoomInfos room = null;

            if (m_roomdata.ContainsKey(game.Info.GameName))
            {
                room = m_roomdata[game.Info.GameName];
                room.hasStarted = true;

            }
            if (room == null)
                return;
            SendPacket(HubServerPackets.StartRoom, Encoding.UTF8.GetBytes(room.GetRoomName()));
        }

        private void OnRoomRemoved(object sender, EventArgs e)
        {
            Game game = (Game)sender;
            RoomInfos room = null;

            if (m_roomdata.ContainsKey(game.Info.GameName))
            {
                room = m_roomdata[game.Info.GameName];
                m_roomdata.Remove(game.Info.GameName);
            }
            if (room == null)
                return;
            SendPacket(HubServerPackets.RemoveRoom, Encoding.UTF8.GetBytes(room.GetRoomName()));
        }

        private bool CheckDisconnected()
        {
            return (m_client.Client.Poll(1, SelectMode.SelectRead) && m_client.Available == 0);
        }

        private void OnSend(IAsyncResult ar)
        {
            try
            {
                TcpClient client = (TcpClient)ar.AsyncState;
                client.Client.EndSend(ar);
            }
            catch
            {
                Disconnect();
            }
        }

        public void Disconnect()
        {
            if (IsConnected)
                IsConnected = false;
            try
            {
                m_client.Close();
            }
            catch (Exception) { }

            Log.Write(typeof(DevProProtocol).Name, "Disconnected from hubserver");
        }
    }

    public class MessageReceived
    {
        public HubClientPackets Packet { get; private set; }
        public byte[] Raw { get; private set; }
        public BinaryReader Reader { get; private set; }

        public MessageReceived(HubClientPackets packet, byte[] raw, BinaryReader reader)
        {
            Packet = packet;
            Raw = raw;
            Reader = reader;
        }
    }

    public class PacketCommand
    {
        public string Command { get; set; }
        public string Data { get; set; }
    }

    public class LoginRequest
    {
        public string Username { get; set; }
        public string Password { get; set; }
        public int Version { get; set; }
        public string UID { get; set; }
    }

    public class RoomInfos
    {
        public int banListType { get; set; }
        public int timer { get; set; }
        public int rule { get; set; }
        public int mode { get; set; }

        public bool enablePriority { get; set; }
        public bool isNoCheckDeck { get; set; }
        public bool isNoShuffleDeck { get; set; }
        public bool isLocked { get; set; }
        public bool isRanked { get; set; }
        public bool isPreReleaseMode { get; set; }
        public bool isIllegal { get; set; }
        public bool hasStarted { get; set; }

        public int startLp { get; set; }
        public int startHand { get; set; }
        public int drawCount { get; set; }

        public string roomName { get; set; }
        public string[] playerList { get; set; }
        public int[] eloList { get; set; }
        public string hash { get; set; }
        public string server { get; set; }

        public static RoomInfos FromName(string roomName, string serverName)
        {
            RoomInfos infos = new RoomInfos();

            try
            {

                string rules = roomName.Substring(0, 6);

                infos.rule = int.Parse(rules[0].ToString());
                infos.mode = int.Parse(rules[1].ToString());
                infos.timer = int.Parse(rules[2].ToString());
                infos.enablePriority = rules[3] == 'T' || rules[3] == '1';
                infos.isNoCheckDeck = rules[4] == 'T' || rules[4] == '1';
                infos.isNoShuffleDeck = rules[5] == 'T' || rules[5] == '1';

                string data = roomName.Substring(6, roomName.Length - 6);

                if (!data.Contains(",")) return null;

                string[] list = data.Split(',');

                infos.startLp = int.Parse(list[0]);
                infos.banListType = int.Parse(list[1]);

                infos.startHand = int.Parse(list[2]);
                infos.drawCount = int.Parse(list[3]);
                infos.timer = 0;

                // "UL"
                if (list[4].Contains("L"))
                    infos.isLocked = true;

                if (Config.GetBool("DevRanking") && list[4] == "R")
                {
                    infos.isRanked = true;
                }
                else
                {
                    infos.isRanked = false;
                }

                if (infos.isRanked)
                {
                    //dont allow prerelease cards in ranked
                    infos.rule = infos.rule & ~0x4;
                    infos.banListType = infos.rule == 0 ? 1 : 0;
                }

                infos.roomName = (list[5] == "" ? GenerateroomName() : list[5]);

                if ((infos.rule & 0x4) > 0)
                    infos.isPreReleaseMode = true;

                if (infos.isNoCheckDeck || infos.isNoShuffleDeck ||
                    (infos.mode == 2) ? infos.startLp != 16000 : infos.startLp != 8000 || infos.startHand != 5 || infos.drawCount != 1
                    || infos.enablePriority
                    )
                    infos.isIllegal = true;
                else
                    infos.isIllegal = false;
               
                infos.server = serverName;
                infos.playerList = new string[infos.mode == 2 ? 4 : 2];
                infos.eloList = new int[infos.mode == 2 ? 4 : 2];

                infos.hasStarted = false;

                //check playernames
                if (list.Length >= 7)
                {
                    infos.hash = list[6];
                }
                else
                {
                    infos.hash = string.Empty;
                }

            }
            catch (Exception)
            {
                infos.mode = Config.GetInt("Mode");
                infos.rule = Config.GetInt("Rule");
                infos.enablePriority = Config.GetBool("EnablePriority");
                infos.isNoCheckDeck = Config.GetBool("NoCheckDeck");
                infos.isNoShuffleDeck = Config.GetBool("NoShuffleDeck");
                infos.startLp = Config.GetInt("StartLp", 8000);
                infos.banListType = Config.GetInt("Banlist");

                infos.startHand = Config.GetInt("StartHand", 5);
                infos.drawCount = Config.GetInt("DrawCount", 1);

                infos.isRanked = false;
                infos.isLocked = false;
                infos.isPreReleaseMode = true;
                infos.isIllegal = false;

                infos.hash = string.Empty;

                infos.server = serverName;
                infos.eloList = new int[infos.mode == 2 ? 4 : 2];
                infos.playerList = new string[infos.mode == 2 ? 4:2];
                infos.roomName = GenerateroomName();
                infos.hasStarted = false;
                return infos;
            }

            return infos;
        }

        public static string GenerateroomName()
        {
            Guid g = Guid.NewGuid();
            string guidString = Convert.ToBase64String(g.ToByteArray());
            guidString = guidString.Replace("=", "");
            guidString = guidString.Replace("+", "");
            guidString = guidString.Replace("/", "");
            return guidString.Substring(0, 5);
        }

        public string GetRoomName()
        {
            return server + "-" + roomName;
        }
    }
}
