using System;

namespace YGODuelManager
{
    public static class Log
    {
        //ToDo write to file
        public static void Write(string text)
        {
            Console.WriteLine(text);
        }
        public static void Write(string tag, string text)
        {
            Write("[" + tag + "] " + text);
        }
        public static void ServerLog(string text)
        {
            Write("Server", text);
        }
    }
}
