using System;
using System.Configuration;
using System.Text;
using Devbridge.AzureMessaging.Sample.Common;

namespace Devbridge.AzureMessaging.Sample.Client
{
    class Program
    {
        static void Main(string[] args)
        {
            var queueClientFactory = new QueueClientFactory(ConfigurationManager.AppSettings["ServiceBusConnectionString"]);
            var clientFactory = new AzureMessageClientFactory(queueClientFactory);
            var client = clientFactory.CreateMessageQueueClient();

            string lineText;
            Console.WriteLine("Client started. To stop client press escape key.");
            Console.Write("Send message to server: ");
            while (TryReadLine(out lineText))
            {
                Console.WriteLine();
                Console.Write("Send message to server: ");
                client.Publish(new GreetSampleMessage { Text = lineText });
            }
        }

        private static bool TryReadLine(out string result)
        {
            var buf = new StringBuilder();
            while (true)
            {
                var key = Console.ReadKey(true);
                if (key.Key == ConsoleKey.Escape)
                {
                    result = "";
                    return false;
                }

                if (key.Key == ConsoleKey.Enter)
                {
                    result = buf.ToString();
                    return true;
                }

                if (key.Key == ConsoleKey.Backspace)
                {
                    if (buf.Length > 0)
                    {
                        buf.Remove(buf.Length - 1, 1);
                        Console.Write("\b \b");
                    }
                }
                else if (key.KeyChar != 0)
                {
                    buf.Append(key.KeyChar);
                    Console.Write(key.KeyChar);
                }
            }
        }
    }
}
