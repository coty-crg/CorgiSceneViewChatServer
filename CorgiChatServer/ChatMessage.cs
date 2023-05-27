using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CorgiChatServer
{
    [System.Serializable]
    public class ChatMessage
    {
        public string username;
        public string message;
        public long timestamp;
    }
}
