using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace YGODuelManager
{
    public class GameRequest
    {
        public Player User { get; private set; }
        public GameInfo Info { get; private set; }

        public GameRequest(Player player, GameInfo info)
        {
            User = player;
            Info = info;
        }
    }
    public static class GameManager
    {
        //private static bool Recycle = false;
        private static Dictionary<string, Game> _rooms = new Dictionary<string, Game>();
        private static Queue<Game> _openRooms = new Queue<Game>();
        private static Queue<Game> _removedRooms = new Queue<Game>();
        private static Queue<int> _availablePorts = new Queue<int>();
        private static Queue<GameRequest> _requests = new Queue<GameRequest>();

        public static void Init()
        {
            Log.ServerLog("Generating available ports");
            GeneratePortList();
            Log.ServerLog("Preloading Games");
            PreloadGames(); 
        }

        public static void PreloadGames()
        {
            int preload = Config.GetInt("CoreBuffer", 30);
            while(_openRooms.Count < preload  && _availablePorts.Count > 0)
            {
                int port = _availablePorts.Dequeue();
                try
                {
                    Game game = new Game(port);
                    _openRooms.Enqueue(game);

                }
                catch(Exception)
                {
                    Log.ServerLog("Failed to load game on port: " + port);
                }
            }
        }

        private static Game CreateGame(int port)
        {
            return new Game(port);
        }

        public static void RemoveGame(Game game)
        {
            lock (_removedRooms)
                _removedRooms.Enqueue(game);
        }

        public static bool CreateRoom(Player player, GameInfo info)
        {
            if (Program.IsExiting)
            {
                //player.SendRoomMessage("[Shutting Down]");
                return false;
            }
            if(_rooms.ContainsKey(info.GameName))
            {
                //player.SendRoomMessage("[Room Exists]");
                return false;
            }

            //player.SendRoomMessage("[Joining Game]");
            lock (_requests)
                _requests.Enqueue(new GameRequest(player, info));

            return true;
        }

        public static Game RequestRoom(Player player, GameInfo info)
        {
            if (Program.IsExiting)
            {
                //player.SendRoomMessage("[Shutting Down]");
                return null;
            }

            if (_rooms.ContainsKey(info.GameName))
            {
                //if (_rooms[info.GameName].CoreClosed)
                //    player.SendRoomMessage("[Game Finished]");
                //else
                //    return _rooms[info.GameName];
            }
            else
            {
                //player.SendRoomMessage("[Joining Game]");
                lock (_requests)
                    _requests.Enqueue(new GameRequest(player, info));
            }
            return null;
        }

        public static void Handle()
        {
            //remove games no longer required or crashed/recycle them back into avliable game rooms

            while(_removedRooms.Count > 0)
            {
                Game game;

                lock (_removedRooms)
                    game = _removedRooms.Dequeue();
                if (_rooms.ContainsKey(game.Info.GameName))
                {
                    _rooms.Remove(game.Info.GameName);
                    AddonsManager.RoomClose(game);
                    Log.Write("Room Removed: " + game.Info.GameName);
                }

                _availablePorts.Enqueue(game.Port);//reqired for loading new cores
            }

            //handle player join requests
            while(_openRooms.Count > 0 && _requests.Count > 0)
            {
                GameRequest request;
                Game game = null;

                lock (_requests)
                    request = _requests.Dequeue();
                
                //double check the room hasnt spawned.. while waiting
                if (_rooms.ContainsKey(request.Info.GameName))
                    game = _rooms[request.Info.GameName];
                if (game == null)
                {
                    lock (_openRooms)
                        game = _openRooms.Dequeue();
                    game.UpdateGameInfo(request.Info);
                    _rooms.Add(request.Info.GameName, game);
                    //Log.Write("Room Created: " + request.Info.GameName);
                }
                //request.User.JoinGame(game);
            }

            //now all the rooms have been removed update the active ones
            //foreach (Game game in _openRooms)
            //    game.Update();
            //foreach (string key in _rooms.Keys)
            //    _rooms[key].Update();

        }

        private static void GeneratePortList()
        {
            int port = Config.GetInt("PortStartRange", 9000);

            while (_availablePorts.Count < Config.GetInt("MaxPorts", 1000))
            {
                try
                {
                    TcpListener listener = new TcpListener(IPAddress.Loopback, port);
                    listener.Start();
                    listener.Stop();
                    _availablePorts.Enqueue(port);
                    port++;
                }
                catch (Exception)
                {
                    //failed port++
                    port++;
                }
                Thread.Sleep(1);
            }
        }

        public static void CloseGames()
        {
            foreach(string key in _rooms.Keys)
                _rooms[key].CloseCore();
        }
    }
}
