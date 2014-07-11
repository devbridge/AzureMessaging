namespace Devbridge.AzureMessaging.Interfaces
{
    public interface IAzureMessageHandler
    {
        string QueueName { get; set; }
        object ProcessMessage(IAzureMessage message);
    }
}