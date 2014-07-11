using System;
using System.Collections.Generic;
using System.IO;
using Devbridge.AzureMessaging.Extensions;
using Devbridge.AzureMessaging.Interfaces;
using Microsoft.ServiceBus.Messaging;

namespace Devbridge.AzureMessaging
{
    public class AzureMessageQueueClient : IMessageQueueClient
    {
        private readonly Dictionary<string, IAzureQueueClient> queueClients = new Dictionary<string, IAzureQueueClient>();
        private readonly IAzureQueueClientFactory queueClientFactory;

        public AzureMessageQueueClient(IAzureQueueClientFactory queueClientFactory)
        {
            this.queueClientFactory = queueClientFactory;
        }

        public void Dispose()
        {
            // Do nothing
        }

        public void Publish<T>(T messageBody)
        {
            if (typeof(IMessage).IsAssignableFrom(typeof(T)))
            {
                Publish((IMessage<T>)messageBody);
            }
            else
            {
                Publish<T>(new Message<T>(messageBody));
            }
        }

        private IAzureQueueClient GetQueueClient(string queueName)
        {
            if (queueClients.ContainsKey(queueName))
            {
                return queueClients[queueName];
            }

            var client = queueClientFactory.Create(queueName, true);

            queueClients.Add(queueName, client);

            return client;
        }

        public void Publish<T>(IMessage<T> message)
        {
            var queueName = typeof(T).QueueName();
            var client = GetQueueClient(queueName);

            client.Send(message.SerializeToString());
        }

        public void PublishAsync<T>(IMessage<T> message)
        {
            var queueName = typeof(T).QueueName();
            var client = GetQueueClient(queueName);
            var brokeredMessage = new BrokeredMessage(message.SerializeToString());

            client.SendAsync(brokeredMessage);
        }

        public void Publish(string queueName, byte[] messageBytes)
        {
            throw new NotImplementedException();
        }

        public void Notify(string queueName, byte[] messageBytes)
        {
            throw new NotImplementedException();
        }

        public byte[] Get(string queueName, TimeSpan? timeOut)
        {
            var client = GetQueueClient(queueName);
            var message = client.Receive();

            if (message == null)
            {
                return null;
            }

            var stream = message.GetBody<Stream>();

            return ReadFully(stream);
        }

        public byte[] GetAsync(string queueName)
        {
            var client = GetQueueClient(queueName);
            var message = client.Receive();

            if (message == null)
            {
                return null;
            }

            var stream = message.GetBody<Stream>();

            return ReadFully(stream);
        }

        public static byte[] ReadFully(Stream input)
        {
            using (var ms = new MemoryStream())
            {
                input.CopyTo(ms);
                return ms.ToArray();
            }
        }

        public string WaitForNotifyOnAny(params string[] channelNames)
        {
            throw new NotImplementedException();
        }
    }
}