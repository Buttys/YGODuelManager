using System;

namespace YGODuelManager.Events
{
    public class PlayerEventArgs : EventArgs
    {
        public Player Player { get; private set; }

        public PlayerEventArgs(Player player)
        {
            Player = player;
        }
    }
}
