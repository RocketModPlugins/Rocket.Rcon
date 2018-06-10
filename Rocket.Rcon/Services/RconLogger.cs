using System;
using Rocket.API.DependencyInjection;
using Rocket.API.Logging;
using Rocket.API.User;

namespace Rocket.Rcon.Services
{
    public class RconLogger : ILogger
    {
        private readonly RconServer _rconServer;

        public RconLogger(IDependencyContainer container)
        {
            _rconServer = (RconServer) container.Resolve<IUserManager>("rcon");
        }

        public string ServiceName => "RconLogger";
        public void Log(string message, LogLevel level = LogLevel.Information, Exception exception = null, params object[] arguments)
        {
            _rconServer.Broadcast(null, $"[{DateTime.Now:t}] [{level}] {message}", null, arguments);
        }

        public bool IsEnabled(LogLevel level)
        {
            return true;
        }
    }
}