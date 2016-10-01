using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using YGOSharp.Network.Enums;

namespace YGODuelManager
{
    public class Game
    {
        public int Port { get; private set; }
        public GameInfo Info { get; private set; }
        private Process Core;
        public GameState State { get; private set; }
        //private StreamReader Stream;
        private List<Player> _clients;
        public Player Host { get; private set; }
        private Player[] Players;
        
        public bool CoreClosed { get { return Core.HasExited; } }


        public Game(int port)
        {
            _clients = new List<Player>();
            Info = new GameInfo();
            State = GameState.Lobby;
            Players = new Player[Info.IsTag ? 4 : 2];
            Port = port;
            LoadCore();
        }

        private void LoadCore()
        {
            string core = Config.GetString("CoreEXE", "YGOSharp.exe");
            string dir = Config.GetString("CoreDir", string.Empty);
            if (File.Exists(dir + core))
            {
                Core = new Process() { EnableRaisingEvents = true };
                Core.StartInfo.FileName = Path.Combine(dir , core);
                Core.StartInfo.WorkingDirectory = dir;
                //Core.StartInfo.UseShellExecute = false;
                //Core.StartInfo.RedirectStandardOutput = true;
                //Core.OutputDataReceived += OutputStream;
                Core.StartInfo.Arguments = "Port=" + Port;//+ " YRP2=true";
//#if !DEBUG
                Core.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
//#endif
                
                Core.Exited += OnClose;
                Core.Start();
                //Core.BeginOutputReadLine();
                //need to consider adding somthing that will wait to confirm the core loaded correctly
                //sometimes the core will open fine but close right after

            }
        }

        private void OutputStream(object sender, DataReceivedEventArgs e)
        {
            //Log.Write(e.Data);
        }

        public void UpdateGameInfo(GameInfo info)
        {
            if (State == GameState.Lobby)
            {
                Info = info;
                Players = new Player[Info.IsTag ? 4:2];
            }
        }

        public void UpdateGameState(Player player, GameState state)
        {
            if (player.Equals(Host))
            {
                State = state;
                if (State == GameState.Starting)
                    GameManager.RoomStart(this);
            }
        }

        public void JoinGame(Player player)
        {
            if (Host == null)
                Host = player;
            lock (_clients)
                _clients.Add(player);
        }

        public void LeaveGame(Player player)
        {
            lock (_clients)
                _clients.Remove(player);
            if (player.Equals(Host))
            {
                GameManager.RemoveGame(this);
                State = GameState.End;
            }
            else
                GameManager.RoomLeft(this);
        }

        private bool Created;
        public void PlayerTypeChange(Player player, BinaryReader packet)
        {
            if (State != GameState.Lobby)
                return;

            int type = packet.ReadByte();
            int playerPos = type & 0xF;
            int max = Info.IsTag ? 4 : 2;

            if (playerPos < max)
            {
                Players[playerPos] = player;

                //now a player has offically joined let everyone know
                if (!Created)
                {
                    GameManager.RoomCreated(this);
                    Created = true;
                }
                else
                    GameManager.RoomJoin(this);
            }
        }

        public string[] PlayerList()
        {
            List<string> players = new List<string>();

            foreach (Player player in Players)
                players.Add(player == null ? "???" : player.Name);

            return players.ToArray();
        }

        private void ResetGame()
        {
            //ToDo make core reusable
        }

        public void CloseCore()
        {
            if(!CoreClosed)
                Core.Close();
        }

        private void OnClose(object sender, EventArgs e)
        {
            if (State != GameState.End)
                GameManager.RemoveGame(this);
            if (!Created)
                Log.Write("Unexpected core crash.");
        }
    }
}
