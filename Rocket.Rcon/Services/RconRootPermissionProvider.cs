using System;
using Rocket.API.User;
using Rocket.Core.Permissions;
using Rocket.Core.ServiceProxies;

namespace Rocket.Rcon.Services
{
    [ServicePriority(Priority = ServicePriority.High)]
    public class RconRootPermissionProvider : FullPermitPermissionProvider
    {
        public override bool SupportsTarget(IIdentity target)
        {
            return target is RconConnection && target.Name.Equals("root", StringComparison.OrdinalIgnoreCase);
        }
    }
}