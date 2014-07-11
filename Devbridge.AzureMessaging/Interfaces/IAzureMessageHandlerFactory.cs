namespace Devbridge.AzureMessaging.Interfaces
{
    public interface IAzureMessageHandlerFactory
    {
        IAzureMessageHandler CreateMessageHandler();
    }
}