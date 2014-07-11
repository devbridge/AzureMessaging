using System;

namespace Devbridge.AzureMessaging.Interfaces
{
	public interface IMessageQueueClientFactory
		: IDisposable
	{
		IMessageQueueClient CreateMessageQueueClient();
	}
}