using System;
using System.Text;

namespace CorgiChatServer
{
    public class CLI
    {
        public Dictionary<string, System.Action<string[]>> commands = new Dictionary<string, System.Action<string[]>>();

        public void ConfigureCommands()
        {
            commands.Clear();
            commands.Add("help", HelpCommand);
            commands.Add("exit", args => Program.running = false);
            commands.Add("printmessages", PrintMessagesCommand);
            commands.Add("say", SendGlobalMessageCommand);
        }

        public void Listen()
        {
            ConfigureCommands();

            while (Program.running)
            {
                Thread.Sleep(10);

                // try
                // {
                //     // console stream is null if running from systemd 
                //     if(Console.In == null)
                //     {
                //         continue;
                //     }
                // 
                //     var input = Console.In.ReadLine();
                //     var args = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                // 
                //     if (args.Length > 0 && commands.TryGetValue(args[0], out Action<string[]> action))
                //     {
                //         action.Invoke(args);
                //     }
                //     else
                //     {
                //         Console.WriteLine("Command not found.");
                //     }
                // }
                // catch (System.Exception e)
                // {
                //     Console.WriteLine(e.Message);
                //     Console.WriteLine(e.StackTrace);
                // }
            }
        }

        public static void HelpCommand(string[] args)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Commands:");

            var commands = Program.cli.commands;
            foreach (var entry in commands)
            {
                var command = entry.Key;
                sb.AppendLine(command);
            }

            Console.WriteLine(sb.ToString());
        }

        public static void PrintMessagesCommand(string[] args)
        {
            Program.chatServer.PrintMessages = !Program.chatServer.PrintMessages;
            Console.WriteLine($"PrintMessages: {Program.chatServer.PrintMessages}");
        }

        public static void SendGlobalMessageCommand(string[] args)
        {
            var message = string.Empty;

            for(var i = 1; i < args.Length; ++i)
            {
                message += $"{args[i]} ";
            }

            ChatServer.SendGlobalChatMessage(message); 
        }
    }
}