using System.Collections;
using System.Collections.Generic;

namespace CorgiChatServer
{
    [System.Serializable]
    public struct NetworkMessageHeader
    {
        public int NextMessageSize;
        public NetworkMessageId NextMessageId;
    }
}
