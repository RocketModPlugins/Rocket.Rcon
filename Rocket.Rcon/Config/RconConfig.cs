using Rocket.Core.Configuration;

namespace Rocket.Rcon.Config
{
    public class RconConfig
    {
        public string BindIp { get; set; } = "0.0.0.0";
        public ushort ListenPort { get; set; } = 8999;

        [ConfigArray(ElementName = "RconUser")]
        public RconUser[] RconUsers { get; set; } =
        {
            new RconUser
            {
                Name = "root",
                Password = "changeme"
            }
        };

        public uint MaxConcurrentConnections { get; set; } = 10;
    }
}