using System.Collections.Generic;
using YGODuelManager.Addons;

namespace YGODuelManager
{
    public class AddonsManager
    {
        public List<AddonBase> Addons { get; private set; }

        public AddonsManager(CoreServer server)
        {
            Addons = new List<AddonBase>();
            //this is just an example. do not use
            //if(Config.GetBool("DevProProtocol"))
            //    Addons.Add(new DevProProtocol(server));
        }

        public void Update()
        {
            foreach (AddonBase addon in Addons)
                addon.Update();
        }
    }
}
