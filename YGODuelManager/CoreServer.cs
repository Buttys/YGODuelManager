using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using YGOSharp.Network;

namespace YGODuelManager
{
    public class CoreServer
    {
        public static int DEFAULT_PORT = 7911;

        public bool IsRunning { get; private set; }
        public bool IsListening { get; private set; }

        private NetworkServer _listener;
        public AddonsManager Addons { get; private set; }
        private readonly List<Player> _clients = new List<Player>();

        private bool _closePending;

        public bool Start()
        {
            if (IsRunning)
                return false;
            try
            {
                _listener = new NetworkServer(IPAddress.Any, Config.GetInt("Port", DEFAULT_PORT));
                _listener.ClientConnected += Listener_ClientConnected;
                _listener.Start();
                IsRunning = true;
                IsListening = true;
                Addons = new AddonsManager(this);
            }
            catch (Exception)
            {
                return false;
            }
            return true;
        }

        public void StopListening()
        {
            if (!IsListening)
                return;
            IsListening = false;
            _listener.Close();
        }

        public void Stop()
        {
            if (IsListening)
                StopListening();
            foreach (Player client in _clients)
                client.Disconnect();
            //Game.Stop();
            IsRunning = false;
        }

        public void StopDelayed()
        {
            _closePending = true;
            foreach (Player client in _clients)
                client.Disconnect();
        }

        public void AddClient(YGOClient client)
        {
            Player player = new Player(client);
            _clients.Add(player);

            client.PacketReceived += packet => player.Parse(packet);
            client.Disconnected += packet => player.OnDisconnected();
        }

        public void Tick()
        {
            _listener.Update();

            List<Player> disconnectedClients = new List<Player>();

            foreach (Player client in _clients)
            {
                if (!client.Update())
                    disconnectedClients.Add(client);
            }

            while (disconnectedClients.Count > 0)
            {
                _clients.Remove(disconnectedClients[0]);
                disconnectedClients.RemoveAt(0);
            }

            if (_closePending && _clients.Count == 0)
                Stop();

            Addons.Update();
        }

        private void Listener_ClientConnected(NetworkClient client)
        {
            AddClient(new YGOClient(client));
#if DEBUG
            Log.ServerLog("Client Connected: " + client.RemoteIPAddress);
#endif
        }
    }
}
