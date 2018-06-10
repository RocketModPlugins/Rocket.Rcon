using Rocket.API.DependencyInjection;
using Rocket.API.Logging;
using Rocket.API.Permissions;
using Rocket.API.User;
using Rocket.Rcon.Services;

namespace Rocket.Rcon.Properties
{
    public class DependencyRegistrator : IDependencyRegistrator
    {
        public void Register(IDependencyContainer container, IDependencyResolver resolver)
        {
            container.RegisterSingletonType<IPermissionProvider, RconRootPermissionProvider>("rcon");
            container.RegisterSingletonType<IUserManager, RconServer>("rcon");
            container.RegisterSingletonInstance<ILogger>(new RconLogger(container));
        }
    }
}