using System.Configuration;
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

            client.Publish(new GreetMessage { Text = "Client hello" });
        }
    }
}
