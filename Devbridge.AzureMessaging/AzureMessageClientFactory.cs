using Devbridge.AzureMessaging.Interfaces;

namespace Devbridge.AzureMessaging
{
    public class AzureMessageClientFactory : IMessageFactory
    {
        private readonly IAzureQueueClientFactory queueClientFactory;

        public AzureMessageClientFactory(IAzureQueueClientFactory queueClientFactory)
        {
            this.queueClientFactory = queueClientFactory;
        }

        public void Dispose()
        {
            // Do nothing
        }

        public IMessageQueueClient CreateMessageQueueClient()
        {
            return new AzureMessageQueueClient(queueClientFactory);
        }

        public IMessageProducer CreateMessageProducer()
        {
            return new AzureMessageQueueClient(queueClientFactory);
        }
    }
}