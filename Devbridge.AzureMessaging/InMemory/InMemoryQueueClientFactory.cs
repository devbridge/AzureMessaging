using System.Collections.Generic;
using Devbridge.AzureMessaging.Interfaces;

namespace Devbridge.AzureMessaging.InMemory
{
    public class InMemoryQueueClientFactory : IAzureQueueClientFactory
    {
        private readonly Dictionary<string, InMemoryQueueClient> clients = new Dictionary<string, InMemoryQueueClient>();

        readonly object syncLock = new object();

        public IAzureQueueClient Create(string queueName, bool checkIfExists = false)
        {
            InMemoryQueueClient client;

            lock (syncLock)
            {
                if (clients.ContainsKey(queueName))
                {
                    client = clients[queueName];
                }
                else
                {
                    client = new InMemoryQueueClient();

                    clients.Add(queueName, client);
                }
            }

            return client;
        }
    }
}