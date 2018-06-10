using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using Rocket.API;
using Rocket.API.Commands;
using Rocket.API.DependencyInjection;
using Rocket.API.Logging;
using Rocket.API.Plugins;
using Rocket.API.Scheduler;
using Rocket.API.User;
using Rocket.Core.Logging;
using Rocket.Rcon.Config;

namespace Rocket.Rcon.Services
{
    public class RconServer : Socket, IDisposable, IUserManager
    {
        public string ServiceName => "RconUserManager";
        public IEnumerable<IUser> OnlineUsers => _connections.Cast<IUser>();

        private static int _connectionId;
        private readonly List<RconConnection> _connections = new List<RconConnection>();
        private readonly IDependencyContainer _container;
        private readonly ICommandHandler _commandHandler;
        private readonly ILogger _logger;
        private readonly ITaskScheduler _scheduler;
        private readonly IRuntime _runtime;
        private RconConfig _config;
        private TcpListener _listener;
        private Thread _waitingThread;

        private RconPlugin RconPlugin => (RconPlugin)_container.Resolve<IPluginManager>().GetPlugin("Rcon");

        public RconServer(
            IDependencyContainer container,
            ICommandHandler commandHandler,
            ILogger logger,
            ITaskScheduler scheduler,
            IRuntime runtime)
            : base(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
        {
            _container = container;
            _commandHandler = commandHandler;
            _logger = logger;
            _scheduler = scheduler;
            _runtime = runtime;

            IPHostEntry ipHostInfo = Dns.GetHostEntry(Dns.GetHostName());
            IPAddress = ipHostInfo.AddressList.First(o => o.AddressFamily == AddressFamily.InterNetwork);
        }

        public void SetConfig(RconConfig config)
        {
            _config = config;
            Port = config.ListenPort;
        }

        public bool Kick(IUser user, IUser kickedBy = null, string reason = null)
        {
            var conn = (RconConnection)user;
            if (!conn.IsOnline)
                return false;
            conn.Close(reason);
            return true;
        }

        public bool Ban(IUserInfo user, IUser bannedBy = null, string reason = null, TimeSpan? timeSpan = null)
        {
            throw new NotImplementedException();
        }

        public bool Unban(IUserInfo user, IUser unbannedBy = null)
        {
            throw new NotImplementedException();
        }

        public void SendMessage(IUser sender, IUser receiver, string message, Color? color = null, params object[] arguments)
        {
            var conn = (RconConnection)receiver;
            if (conn.Authenticated)
                conn.WriteLine(string.Format(message, arguments));
        }

        public void Broadcast(IUser sender, IEnumerable<IUser> receivers, string message, Color? color = null, params object[] arguments)
        {
            foreach (var user in receivers)
            {
                var conn = (RconConnection)user;
                if (conn.Authenticated)
                    conn.WriteLine(string.Format(message, arguments));
            }
        }

        public void Broadcast(IUser sender, string message, Color? color = null, params object[] arguments)
        {
            foreach (var user in OnlineUsers)
            {
                var conn = (RconConnection)user;
                if (conn.Authenticated)
                    conn.WriteLine(string.Format(message, arguments));
            }
        }

        public IUserInfo GetUser(string id)
        {
            return null; /* not supported */
        }

        public void StartListening()
        {
            if (IsListening)
                return;

            IsListening = true;

            _listener = new TcpListener(IPAddress.Parse(_config.BindIp), _config.ListenPort);
            _listener.Start();

            // Logger.Log("Waiting for new connection...");

            _waitingThread = new Thread(() =>
            {
                while (IsListening)
                {
                    RconConnection connection = new RconConnection(_listener.AcceptTcpClient(), this, _logger, ++_connectionId);
                    _connections.Add(connection);
                    ThreadPool.QueueUserWorkItem(HandleConnection, connection);
                }
            });
            _waitingThread.Start();
        }

        public void Dispose()
        {
            base.Dispose(true);
        }

        protected override void Dispose(bool disposing)
        {
            IsListening = false;
            base.Dispose(disposing);
            Thread.Sleep(10);
        }

        public bool IsListening { get; private set; }

        public void StopListening()
        {
            if (!IsListening)
                return;

            IsListening = false;
        }

        public IPAddress IPAddress { get; }

        public ushort Port { get; private set; } = 8999;

        private void HandleConnection(object state)
        {
            RconConnection connection = (RconConnection)state;
            connection.WriteLine("RocketRcon v" + _runtime.Version);

            try
            {
                int nonAuthCommandCount = 0;
                if (_config.MaxConcurrentConnections > 0 && _connections.Count > _config.MaxConcurrentConnections)
                {
                    connection.Close("Too many clients connected to RCON!");
                    _logger.LogWarning(connection.ConnectionName + ": Maximum RCON connections has been reached.");
                }
                else
                {
                    while (connection.IsOnline)
                    {
                        Thread.Sleep(100);
                        var commandLine = connection.Read();
                        if (commandLine == "")
                            continue;

                        if (!connection.Authenticated)
                        {
                            nonAuthCommandCount++;
                            if (nonAuthCommandCount > 4)
                            {
                                connection.Close("Too many commands sent before authentication!");
                                _logger.LogWarning(connection.ConnectionName + ": Client has sent too many commands before authentication!");
                                break;
                            }
                        }

                        commandLine = commandLine.Trim('\n', '\r', ' ', '\0');
                        if (commandLine == "quit")
                        {
                            connection.Close("Quit.");
                            break;
                        }


                        if (string.IsNullOrEmpty(commandLine))
                            continue;

                        if (commandLine == "login")
                        {
                            connection.WriteLine(connection.Authenticated
                                ? "Notice: You are already logged in!"
                                : "Syntax: login <user> <password>");
                            continue;
                        }

                        var args = commandLine.Split(' ');
                        if (args.Length > 2 && args[0] == "login")
                        {
                            if (connection.Authenticated)
                            {
                                connection.WriteLine("Notice: You are already logged in!");
                                continue;
                            }

                            foreach (var user in _config.RconUsers)
                            {
                                if (args[1].Equals(user.Name, StringComparison.Ordinal) && args[2].Equals(user.Password, StringComparison.Ordinal))
                                {
                                    connection.Authenticated = true;
                                    _logger.LogInformation(connection.ConnectionName + " has logged in.");
                                    break;
                                }
                            }

                            if (connection.Authenticated)
                                continue;

                            connection.Close("Invalid password!");
                            _logger.LogWarning("Client has failed to log in.");
                            break;
                        }


                        if (!connection.Authenticated)
                        {
                            connection.WriteLine("Error: You have not logged in yet! Login with syntax: login <username> <password>");
                            continue;
                        }

                        //execute command on main thread
                        _scheduler.ScheduleNextFrame(RconPlugin,
                            () => _commandHandler.HandleCommand(connection, commandLine, ""), "RconCommandExecutionTask");
                    }
                }
                _connections.Remove(connection);
                _logger.LogInformation(connection.ConnectionName + " has disconnected.");
                connection.Client.Close();
            }
            catch (Exception ex)
            {
                _logger.LogError(connection.ConnectionName + " caused error:", ex);
            }
        }
    }
}