namespace Devbridge.AzureMessaging.Interfaces
{
    public interface IAzureQueueClientFactory
    {
        IAzureQueueClient Create(string queueName, bool checkIfExists = false);
    }
}