using Rocket.API.DependencyInjection;
using Rocket.Core.Plugins;
using Rocket.API.User;
using Rocket.Rcon.Config;
using Rocket.Rcon.Services;

namespace Rocket.Rcon
{
    public class RconPlugin : Plugin<RconConfig>
    {
        public RconServer Server { get; private set; }

        public RconPlugin(IDependencyContainer container) : base("Rcon", container)
        {
        }

        protected override void OnLoad(bool isFromReload)
        {
            base.OnLoad(isFromReload);
            Server = (RconServer) Container.Resolve<IUserManager>("rcon");
            Server.SetConfig(ConfigurationInstance);
            Server.StartListening();
        }

        protected override void OnUnload()
        {
            base.OnUnload();
            Server.Dispose();
        }
    }
}
