#if !DEBUG
using System;
using System.IO;
#endif
using System.Runtime.InteropServices;
using System.Threading;



namespace YGODuelManager
{
    public class Program
    {
        public static uint ClientVersion = 0x1339;
        private static CoreServer server;
        public static bool IsExiting;

        public static void Main(string[] args)
        {
#if !DEBUG
            try
            {
#endif
            Log.Write("YGODuelManager - Alpha Build");
            Config.Load(args);

            ClientVersion = Config.GetUInt("ClientVersion", ClientVersion);

            GameManager.Init();
            server = new CoreServer();
            
            if (server.Start())
                Log.ServerLog("Listening on port " + CoreServer.DEFAULT_PORT);
            else
            {
                Log.ServerLog("Unable to listen on port " + CoreServer.DEFAULT_PORT);
                Thread.Sleep(10000);
            }
            while (server.IsRunning)
            {
                server.Tick();
                GameManager.Handle();
                Thread.Sleep(1);
            }
#if !DEBUG
            }
            catch (Exception ex)
            {
                File.WriteAllText("crash_" + DateTime.UtcNow.ToString("yyyy-MM-dd_HH-mm-ss") + ".txt", ex.ToString());
            }
#endif
        }
    }
}
