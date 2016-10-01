namespace YGODuelManager
{
    public abstract class AddonBase
    {
        public CoreServer Server { get; private set; }

        protected AddonBase(CoreServer server)
        {
            Server = server;
        }

        public virtual void Update() { }
    }
}
