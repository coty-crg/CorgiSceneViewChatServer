using System;
using System.Net;
using System.Net.Sockets;

namespace CorgiChatServer 
{
    internal class Program
    {
        public const int ServerPort = 8008;

        public static ChatServer chatServer;
        public static CLI cli;
        public static bool running;

        static void Main(string[] args)
        {
            running = true;

            Console.WriteLine("Initializing.");

            var localAddress = GetLocalIpAddress(AddressFamily.InterNetwork);

            if(args.Length == 1)
            {
                var parseIpStr = args[0];

                Console.WriteLine($"Found input ip to bind: {parseIpStr}");

                if(IPAddress.TryParse(parseIpStr, out var parsedAddress))
                {
                    localAddress = parsedAddress;
                }
            }

            var localEndpoint = new IPEndPoint(localAddress, ServerPort);

            chatServer = new ChatServer();
            chatServer.Initialize(localEndpoint);

            Console.WriteLine($"Ready! Listening at {localEndpoint}.");

            cli = new CLI();
            cli.Listen();

            Console.WriteLine("Shut down.");
        }

        public static IPAddress GetLocalIpAddress(AddressFamily family)
        {
            var ourHostname = Dns.GetHostName();
            var ourAddresses = Dns.GetHostAddresses(ourHostname);

            for (var i = 0; i < ourAddresses.Length; ++i)
            {
                var address = ourAddresses[i];

                if (address.AddressFamily == family)
                {
                    return address;
                }
            }

            return null;
        }
    }
}