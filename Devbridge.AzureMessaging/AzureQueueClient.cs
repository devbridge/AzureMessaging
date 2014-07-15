using System;
using Common.Logging;
using Devbridge.AzureMessaging.Extensions;
using Devbridge.AzureMessaging.Interfaces;
using Microsoft.ServiceBus.Messaging;

namespace Devbridge.AzureMessaging
{
    public class AzureQueueClient : IAzureQueueClient
    {
        private readonly QueueClient queueClient;

        private static readonly ILog Log = LogManager.GetCurrentClassLogger();

        public AzureQueueClient(QueueClient queueClient)
        {
            this.queueClient = queueClient;
        }

        public void Send(string message)
        {
            Send(new BrokeredMessage(message));
        }

        public void Send(BrokeredMessage message)
        {
            queueClient.Send(message);
        }

        public void SendAsync(BrokeredMessage message)
        {
            queueClient.BeginSend(message, x => ProcessEndSend(x, message), queueClient);
        }

        public IAzureMessage Receive()
        {
            var message = queueClient.Receive();

            return message == null ? null : new AzureMessage(message);
        }

        private static void ProcessEndSend(IAsyncResult result, object message)
        {
            try
            {
                var qc = result.AsyncState as QueueClient;
                if (qc != null)
                {
                    qc.EndSend(result);
                }
            }
            catch (Exception e)
            {
                Log.Error("Thrown {0} while processing message: {1}".Fmt(e.GetType().Name, message), e);
            }
        }
    }
}