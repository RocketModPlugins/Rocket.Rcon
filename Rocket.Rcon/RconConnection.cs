using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Rocket.API.Logging;
using Rocket.API.User;
using Rocket.Core.Logging;

namespace Rocket.Rcon
{
    public class RconConnection : IUser
    {
        private readonly ILogger _logger;
        public TcpClient Client { get; set; }
        public bool Authenticated { get; set; }
        public string Username { get; set; }

        public EndPoint RemoteEndPoint { get; }

        public RconConnection(TcpClient client, IUserManager userManager, ILogger logger, int connectionId)
        {
            _logger = logger;
            Client = client;
            RemoteEndPoint = client.Client.RemoteEndPoint;
            Authenticated = false;
            SessionConnectTime = DateTime.Now;
            UserManager = userManager;
            ConnectionId = connectionId;
        }

        public void Write(string text)
        {
            byte[] data = new UTF8Encoding().GetBytes(text);
            try
            {
                if (Client.Client.Connected)
                    Client.GetStream().Write(data, 0, data.Length);
            }
            catch (Exception ex)
            {
                _logger.LogError(ConnectionName + " caused an exception:", ex);
            }
        }

        public void WriteLine(string text)
        {
            Write(text + "\r\n");
        }

        public string Read()
        {
            byte[] buffer = new byte[1];
            List<byte> dataArray = new List<byte>();
            string input = "";
            int loopCount = 0;
            int skipCount = 0;

            try
            {
                if (Client.Client.Connected)
                {
                    NetworkStream stream = Client.GetStream();
                    while (true)
                    {
                        loopCount++;
                        if (loopCount > 2048)
                            break;

                        int k = stream.Read(buffer, 0, 1);
                        if (k == 0)
                            return "";

                        byte b = buffer[0];
                        // Ignore Putty connection Preamble.
                        if (!Authenticated && b == 0xFF && skipCount <= 0)
                        {
                            skipCount = 2;
                            continue;
                        }

                        if (!Authenticated && skipCount > 0)
                        {
                            skipCount--;
                            continue;
                        }
                        dataArray.Add(b);
                        // break on \r and \n.
                        if (b == 0x0D || b == 0x0A)
                            break;
                    }
                    // Convert byte array into UTF8 string.
                    input = Encoding.UTF8.GetString(dataArray.ToArray(), 0, dataArray.Count);
                }
            }
            catch (Exception ex)
            {
                // "if" disables error message on Read for lost or force closed connections(ie, kicked by command.).
                _logger.LogError(ConnectionName + " caused an exception:", ex);
                return "";
            }
            return input;
        }

        public void Close(string reason = null)
        {
            if (!IsOnline)
                return;

            if (reason != null)
            {
                WriteLine("Terminated: " + reason);
                Thread.Sleep(1500);
            }

            Client.Close();
            SessionDisconnectTime = DateTime.Now;
        }

        public string ConnectionName => "[" + ConnectionId + "] " + Username + "@" + RemoteEndPoint;

        public string Address => Client.Client.Connected ? Client.Client.RemoteEndPoint.ToString() : "?";
        public string Id => Username;
        public string Name => ConnectionName;
        public string IdentityType => "RCON";
        public IUserManager UserManager { get; }
        public int ConnectionId { get; }
        public bool IsOnline => Client.Connected;
        public DateTime SessionConnectTime { get; }
        public DateTime? SessionDisconnectTime { get; private set; }
        public DateTime? LastSeen => IsOnline ? DateTime.Now : SessionDisconnectTime ?? SessionConnectTime;
        public string UserType => "RconUser";
    }
}