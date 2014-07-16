using System;
using System.Configuration;
using Devbridge.AzureMessaging.Sample.Common;

namespace Devbridge.AzureMessaging.Sample.Server
{
    class Program
    {
        static void Main(string[] args)
        {
            var queueClientFactory = new QueueClientFactory(ConfigurationManager.AppSettings["ServiceBusConnectionString"]);
            var server = new AzureMessageService(queueClientFactory);

            var settings = new MessageHandlerSettings
            {
                NoOfThreads = 2,
                NoOfRetries = 3,
                IntervalBetweenRetries = TimeSpan.FromSeconds(2),
                DuplicateIntervalWithEachRetry = true
            };

            server.RegisterHandler<GreetSampleMessage>(x =>
            {
                var greet = x.GetBody();
                Console.WriteLine("Message from client: " + greet.Text);
 
                return null;
            }, settings);

            server.Start();

            Console.WriteLine("Server started and waiting for messages from clients. To stop server press enter key.");
            Console.ReadLine();

            server.Dispose();
        }
    }
}
