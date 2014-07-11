using System;
using Devbridge.AzureMessaging.Interfaces;
using Microsoft.ServiceBus;
using Microsoft.ServiceBus.Messaging;

namespace Devbridge.AzureMessaging
{
    public class QueueClientFactory : IAzureQueueClientFactory
    {
        private readonly string connectionString;

        public QueueClientFactory(string connectionString)
        {
            this.connectionString = connectionString;
        }

        public IAzureQueueClient Create(string queueName, bool checkIfExists = false)
        {
            var client = QueueClient.CreateFromConnectionString(connectionString, queueName);
            var azureQueueClient = new AzureQueueClient(client);

            if (!checkIfExists)
            {
                return azureQueueClient;
            }

            // Configure Queue Settings
            var namespaceManager = NamespaceManager.CreateFromConnectionString(connectionString);

            // Create queue if not exists:
            if (!namespaceManager.QueueExists(queueName))
            {
                var queueDescription = new QueueDescription(queueName)
                {
                    MaxSizeInMegabytes = 5120,
                    DefaultMessageTimeToLive = TimeSpan.FromDays(7),
                    RequiresDuplicateDetection = false,
                    MaxDeliveryCount = 10
                };

                namespaceManager.CreateQueue(queueDescription);
            }

            return azureQueueClient;
        }
    }
}