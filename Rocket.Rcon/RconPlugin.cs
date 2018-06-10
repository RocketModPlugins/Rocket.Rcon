using Rocket.API.DependencyInjection;
using Rocket.Core.Plugins;
using Rocket.API.User;
using Rocket.Rcon.Config;
using Rocket.Rcon.Services;

namespace Rocket.Rcon
{
    public class RconPlugin : Plugin<RconConfig>
    {
        private RconServer _server;

        public RconPlugin(IDependencyContainer container) : base("Rcon", container)
        {
        }

        protected override void OnLoad(bool isFromReload)
        {
            base.OnLoad(isFromReload);
            _server = (RconServer) Container.Resolve<IUserManager>("rcon");
            _server.SetConfig(ConfigurationInstance);
            _server.StartListening();
        }

        protected override void OnUnload()
        {
            base.OnUnload();
            _server.StopListening();
            _server.Dispose();
        }
    }
}
