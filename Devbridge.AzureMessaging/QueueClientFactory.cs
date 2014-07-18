using System;
using System.Configuration;
using Devbridge.AzureMessaging.Interfaces;
using Microsoft.ServiceBus;
using Microsoft.ServiceBus.Messaging;

namespace Devbridge.AzureMessaging
{
    public class QueueClientFactory : IAzureQueueClientFactory
    {
        private readonly string connectionString;

        public QueueClientFactory(string connectionStringName)
        {
            var connectionStringSettings = ConfigurationManager.ConnectionStrings[connectionStringName];
            if (connectionStringSettings == null)
            {
                throw new ArgumentException("Invalid connection string name has been supplied", "connectionStringName");
            }
            connectionString = connectionStringSettings.ConnectionString;
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
            try
            {
                TryCreateQueueIfNotExists(namespaceManager, queueName);
            }
            catch (MessagingException ex)
            {
                //If queue was deleted recently Service Bus may throw Microsoft.ServiceBus.Messaging.MessagingException. Then we have to retry to create queue.
                if (ex.IsTransient) //Check this property to determine if the operation should be retried.
                {
                    TryCreateQueueIfNotExists(namespaceManager, queueName);
                }
                else
                {
                    throw;
                }
            }

            return azureQueueClient;
        }

        private void TryCreateQueueIfNotExists(NamespaceManager namespaceManager, string queueName)
        {
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
        }
    }
}