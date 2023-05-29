using Microsoft.VisualBasic;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace CorgiChatServer
{
    internal class ChatServer
    {
        private Thread _listenThread;
        private Thread _updateThread;
        private Socket _tcpSocket;

        private IPEndPoint _endpoint;
        private bool _running;

        private ConcurrentQueue<ChatClient> _newClients = new ConcurrentQueue<ChatClient>();
        private List<ChatClient> _connectedClients = new List<ChatClient>();

        private byte[] _receiveBuffer = new byte[1024 * 16];
        private byte[] _sendBuffer = new byte[1024 * 16];

        public bool PrintMessages;

        private static Dictionary<NetworkMessageId, System.Action<ChatClient,NetworkMessage>> networkCallbacks = new Dictionary<NetworkMessageId, Action<ChatClient, NetworkMessage>>()
        {
            { NetworkMessageId.ChatMessage, OnNetworkMessage_ChatMessage },
            { NetworkMessageId.ChangeChannel, OnNetworkMessage_ChangedChannel },
            { NetworkMessageId.SetUsername, OnNetworkMessage_ChangedUsername },
            { NetworkMessageId.OpenedScene, OnNetworkMessage_OpenedScene },
        };

        private static void OnNetworkMessage_ChatMessage(ChatClient client, NetworkMessage networkMessage)
        {
            var chatMessage = (NetworkMessageChatMessage) networkMessage;

            if(Program.chatServer.PrintMessages)
            {
                Console.WriteLine($"[{client.Channel}] ({chatMessage.chatMessage.username}): {chatMessage.chatMessage.message}");
            }
             
            // send this message to everyone on the same channel as this client 
            foreach(var otherClient in Program.chatServer._connectedClients)
            {
                if(otherClient.Channel != client.Channel)
                {
                    continue;
                }

                otherClient._sendQueue.Enqueue(chatMessage);
            }
        }

        private static void OnNetworkMessage_ChangedChannel(ChatClient client, NetworkMessage networkMessage)
        {
            var oldChannel = client.Channel;

            var channelMessage = (NetworkMessageChangeChannel)networkMessage;
            client.Channel = channelMessage.channel;

            var count = CountUsersInChannel(client.Channel);
            var relayMessage = $"You have joined the chat channel '{client.Channel}, there are {count} people here.";
            if (count == 1) relayMessage = $"You are the first in the channel '{client.Channel}'";

            client.SendSystemMessage(relayMessage);

            var joinedMessage = $"{client.Username} has joined the channel.";
            var leftMessage = $"{client.Username} has left the channel.";

            foreach(var otherClient in Program.chatServer._connectedClients)
            {
                if (client == otherClient) continue;

                if(otherClient.Channel == client.Channel)
                {
                    otherClient.SendSystemMessage(joinedMessage);
                }

                if(otherClient.Channel == oldChannel)
                {
                    otherClient.SendSystemMessage(leftMessage); 
                }
            }
        }

        private static void OnNetworkMessage_ChangedUsername(ChatClient client, NetworkMessage networkMessage)
        {
            var usernameMessage = (NetworkMessageSetUsername) networkMessage;
            client.Username = usernameMessage.username;
        }

        private static void OnNetworkMessage_OpenedScene(ChatClient client, NetworkMessage networkMessage)
        {
            var message = (NetworkMessageOpenedScene) networkMessage;

            var previousScene = client.SceneName;

            client.SceneName = message.sceneName;

            var prevCount = CountUsersInScene(client.Channel, previousScene);
            var count = CountUsersInScene(client.Channel, client.SceneName);

            if(count <= 1 && prevCount <= 1)
            {
                var joinedMessage = $"You are the only one currently working on {client.SceneName}.";
                client.SendSystemMessage(joinedMessage);
                return; 
            }

            var joinedMessageRelay = $"{client.Username} has entered the scene. There are now {count} people working on {client.SceneName}.";
            var leftMessageRelay = $"{client.Username} is no longer working on this scene. There are now {prevCount} working on {previousScene}";

            foreach (var otherClient in Program.chatServer._connectedClients)
            {
                if (otherClient.Channel != client.Channel) continue;

                if(otherClient.SceneName == client.SceneName)
                {
                    otherClient.SendSystemMessage(joinedMessageRelay);
                }

                if(otherClient.SceneName == previousScene)
                {
                    otherClient.SendSystemMessage(leftMessageRelay);
                }
            }
        }

        public static int CountUsersInScene(string channel, string scene)
        {
            var count = 0;

            foreach(var otherClient in Program.chatServer._connectedClients)
            {
                if(otherClient.Channel == channel && otherClient.SceneName == scene)
                {
                    count++; 
                }
            }

            return count;
        }

        public static int CountUsersInChannel(string channel)
        {
            var count = 0;

            foreach (var otherClient in Program.chatServer._connectedClients)
            {
                if (otherClient.Channel == channel)
                {
                    count++;
                }
            }

            return count;
        }

        public void Initialize(IPEndPoint endpoint)
        {
            _running = true; 
            _endpoint = endpoint;
            
            _tcpSocket = new Socket(_endpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            
            _tcpSocket.Bind(endpoint);
            _tcpSocket.Listen(16);

            _listenThread = new Thread(() => ListenerLoop());
            _listenThread.Start();

            _updateThread = new Thread(() => UpdateLoop());
            _updateThread.Start(); 
        }

        private void ListenerLoop()
        {
            while (_running)
            {
                Thread.Sleep(10);

                try
                {
                    var socket = _tcpSocket.Accept();

                    var newChatClient = new ChatClient()
                    {
                        socket = socket,
                    };

                    _newClients.Enqueue(newChatClient);
                }
                catch (System.Exception e)
                {
                    Console.WriteLine(e.Message);
                    Console.WriteLine(e.StackTrace);
                }
            }
        }

        private void UpdateLoop()
        {
            var removeClientsQueue = new List<ChatClient>();

            while (true)
            {
                Thread.Sleep(10);

                try
                {
                    // fetch new clients from listen thread 
                    while (_newClients.TryDequeue(out var newClient))
                    {
                        _connectedClients.Add(newClient);
                    }

                    // remove any disconnected clients 
                    for (var i = _connectedClients.Count - 1; i >= 0; --i)
                    {
                        var client = _connectedClients[i];
                        if (!IsSocketConnected(client.socket))
                        {
                            client.socket.Dispose();
                            _connectedClients.RemoveAt(i);
                        }
                    }

                    // loop over our connected clients 
                    foreach (var client in _connectedClients)
                    {
                        // receive data
                        if (client.socket.Available >= Serialization.HeaderSize)
                        {
                            var receivedBytes = client.socket.Receive(_receiveBuffer, 0, Serialization.HeaderSize, SocketFlags.Peek);
                            if (receivedBytes >= Serialization.HeaderSize)
                            {
                                var header = Serialization.PeekBuffer_NetworkMessageHeader(_receiveBuffer, 0);

                                if (client.socket.Available >= header.NextMessageSize)
                                {
                                    receivedBytes = client.socket.Receive(_receiveBuffer, 0, Serialization.HeaderSize + header.NextMessageSize, SocketFlags.None);

                                    var readIndex = 0;
                                    var networkMessage = Serialization.ReadBuffer_NetworkMessage(_receiveBuffer, ref readIndex);

                                    if (networkCallbacks.TryGetValue(header.NextMessageId, out var callback))
                                    {
                                        callback.Invoke(client, networkMessage);
                                    }
                                    else
                                    {
                                        Console.WriteLine($"Received unexpected message? {header.NextMessageId}");
                                    }
                                }
                            }
                        }

                        // send data
                        var errorSendingData = false;

                        while (client._sendQueue.TryDequeue(out var sendMessage))
                        {
                            var writeIndex = 0;

                            Serialization.WriteBuffer_NetworkMessage(_sendBuffer, ref writeIndex, sendMessage);

                            try
                            {
                                client.socket.Send(_sendBuffer, 0, writeIndex, SocketFlags.None);
                            }
                            catch (System.Exception e)
                            {
                                Console.WriteLine(e.Message);
                                Console.WriteLine(e.StackTrace);
                                client._sendQueue.Clear(); 
                                errorSendingData = true;
                                break; 
                            }
                        }

                        if(errorSendingData)
                        {
                            removeClientsQueue.Add(client); 
                        }
                    }

                    // remove broken clients 
                    foreach(var client in removeClientsQueue)
                    {
                        _connectedClients.Remove(client);

                        try
                        {
                            client.socket.Dispose(); 
                        }
                        catch (System.Exception e)
                        {
                            Console.WriteLine(e.Message);
                            Console.WriteLine(e.StackTrace);
                        }
                    }

                    removeClientsQueue.Clear();
                }
                catch (System.Exception e)
                {
                    Console.WriteLine(e.Message);
                    Console.WriteLine(e.StackTrace);
                }
            }
        }

        public void Shutdown()
        {
            _running = false; 
        }

        private static bool IsSocketConnected(Socket s)
        {
            var pollCanRead = s.Poll(1000, SelectMode.SelectRead); ;
            var hasNoDataAvailable = s.Available == 0;
            var disconnected = (pollCanRead && hasNoDataAvailable) || !s.Connected;

            return !disconnected;
        }

        public static void SendGlobalChatMessage(string message)
        {
            foreach(var client in Program.chatServer._connectedClients)
            {
                client._sendQueue.Enqueue(new NetworkMessageChatMessage()
                {
                    chatMessage = new ChatMessage()
                    {
                        message = message,
                        timestamp = System.DateTime.UtcNow.Ticks,
                        username = "ChatServer",
                        systemMessage = false,
                    }
                });
            }
        }
    }
}
