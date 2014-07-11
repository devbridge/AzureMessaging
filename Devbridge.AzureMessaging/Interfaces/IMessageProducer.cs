using System;

namespace Devbridge.AzureMessaging.Interfaces
{
	public interface IMessageProducer
		: IDisposable
	{
		void Publish<T>(T messageBody);
		void Publish<T>(IMessage<T> message);
	}

}
