using Microsoft.ServiceBus.Messaging;

namespace Devbridge.AzureMessaging.Interfaces
{
    public interface IAzureQueueClient
    {
        void Send(string message);
        void Send(BrokeredMessage message);
        void SendAsync(BrokeredMessage brokeredMessage);
        IAzureMessage Receive();
    }
}