using System;
using System.Drawing;
using System.Linq;
using Rocket.API.DependencyInjection;
using Rocket.API.Logging;
using Rocket.API.Plugins;
using Rocket.Core.Logging;

namespace Rocket.Rcon.Services
{
    public class RconLogger : ConsoleLogger
    {
        private readonly IDependencyContainer _container;
        private RconServer _rconServer;

        public RconLogger(IDependencyContainer container) : base(container)
        {
            _container = container;
        }

        public override string ServiceName => "RconLogger";

        public override bool IsEnabled(LogLevel level)
        {
            if (level == LogLevel.Game)
                return true;

            return base.IsEnabled(level);
        }

        protected override void WriteColored(string format, Color? color = null, params object[] bindings)
        {
            foreach (var conn in _rconServer.OnlineUsers.Cast<RconConnection>())
            {
                conn.Write(string.Format(format, bindings), color);
            }
        }

        protected override void WriteLineColored(string format, Color? color = null, params object[] bindings)
        {
            foreach (var conn in _rconServer.OnlineUsers.Cast<RconConnection>())
            {
                conn.WriteLine(string.Format(format, bindings), color);
            }
        }

        public override void OnLog(string message, LogLevel level = LogLevel.Information, Exception exception = null, params object[] bindings)
        {
            if (_rconServer == null)
            {
                var plugin = (RconPlugin)_container.Resolve<IPluginManager>().GetPlugin("Rcon");
                _rconServer = plugin?.Server;
            }

            if (_rconServer == null)
                return;

            if (level == LogLevel.Fatal)
                _rconServer.Broadcast(null, "\a");

            base.OnLog(message, level, exception, bindings);
        }
    }
}