namespace YGODuelManager
{
    public class GameInfo
    {
        public uint Banlist { get; set; }
        public int Timer { get; set; }
        public int Rule { get; set; }
        public int Mode { get; set; }

        public bool EnablePriority { get; set; }
        public bool NoCheckDeck { get; set; }
        public bool NoShuffleDeck { get; set; }

        public int StartHand { get; set;  }
        public int DrawCount { get; set; }

        public int Lifepoints { get; set; }

        public string GameName { get; set; }
        public string Password { get; set; }

        public bool IsTag { get { return Mode == 2; } }
        public bool IsMatch { get { return Mode == 1; } }

        public GameInfo()
        {
            Mode = Config.GetInt("Mode");
            Rule = Config.GetInt("Rule");
            EnablePriority = Config.GetBool("EnablePriority");
            NoCheckDeck = Config.GetBool("NoCheckDeck");
            NoShuffleDeck = Config.GetBool("NoShuffleDeck");
            Lifepoints = Config.GetInt("StartLp", 8000);
            Banlist = Config.GetUInt("Banlist");

            StartHand = Config.GetInt("StartHand", 5);
            DrawCount = Config.GetInt("DrawCount", 1);

            GameName = string.Empty;
            Password = string.Empty;
        }
    }
}
