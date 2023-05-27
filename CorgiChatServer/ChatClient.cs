using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace CorgiChatServer
{
    internal class ChatClient
    {
        public Socket socket;
        public string Username;
        public string Channel;

        public ConcurrentQueue<NetworkMessage> _sendQueue = new ConcurrentQueue<NetworkMessage>();
    }
}
