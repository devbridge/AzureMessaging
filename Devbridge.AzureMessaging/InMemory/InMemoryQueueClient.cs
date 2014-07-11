using System;
using System.Collections.Generic;
using System.Threading;
using Devbridge.AzureMessaging.Interfaces;
using Microsoft.ServiceBus.Messaging;

namespace Devbridge.AzureMessaging.InMemory
{
    public class InMemoryQueueClient : IAzureQueueClient
    {
        readonly object syncLock = new object();

        private readonly Queue<string> queue = new Queue<string>();

        public void Send(string message)
        {
            lock (syncLock)
            {
                queue.Enqueue(message);
            }
        }

        public void Send(BrokeredMessage message)
        {
            // Do nothing
        }

        public void SendAsync(BrokeredMessage brokeredMessage)
        {
            throw new NotImplementedException();
        }

        public IAzureMessage Receive()
        {
            var message = GetMessage();

            if (message == null)
            {
                // Pause for 500 miliseconds if queue is empty:
                Thread.Sleep(TimeSpan.FromMilliseconds(500));
            }

            return message;
        }

        private IAzureMessage GetMessage()
        {
            lock (syncLock)
            {
                if (queue.Count == 0)
                {
                    return null;
                }

                var message = queue.Dequeue();

                return new InMemoryAzureMessage(message);
            }
        }
    }
}