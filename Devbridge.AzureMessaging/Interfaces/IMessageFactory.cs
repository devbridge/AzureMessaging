namespace Devbridge.AzureMessaging.Interfaces
{
	public interface IMessageFactory : IMessageQueueClientFactory
	{
		IMessageProducer CreateMessageProducer();
	}
}