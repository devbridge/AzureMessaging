using System;
using System.Collections.Generic;
using Microsoft.ServiceBus.Messaging;

namespace Devbridge.AzureMessaging.Interfaces
{
    public interface IAzureMessage : IDisposable
    {
        T GetBody<T>();
        void Complete();
        void DeadLetter(string deadLetterReason, string deadLetterErrorDescription);
        IDictionary<string, object> Properties { get; }
        void Abandon();
    }

    class InMemoryAzureMessage : IAzureMessage
    {
        private readonly object body;

        public InMemoryAzureMessage(object body)
        {
            Properties = new Dictionary<string, object>();

            this.body = body;
        }

        public void Dispose()
        {
        }

        public T GetBody<T>()
        {
            return (T)body;
        }

        public void Complete()
        {
        }

        public void DeadLetter(string deadLetterReason, string deadLetterErrorDescription)
        {
        }

        public IDictionary<string, object> Properties { get; private set; }

        public void Abandon()
        {
        }
    }

    public class AzureMessage : IAzureMessage
    {
        private readonly BrokeredMessage message;

        public IDictionary<string, object> Properties
        {
            get { return message.Properties; }
        }

        public void Abandon()
        {
            message.Abandon();
        }

        public AzureMessage(BrokeredMessage message)
        {
            this.message = message;
        }

        public T GetBody<T>()
        {
            return message.GetBody<T>();
        }

        public void Complete()
        {
            message.Complete();
        }

        public void DeadLetter(string deadLetterReason, string deadLetterErrorDescription)
        {
            message.DeadLetter(deadLetterReason, deadLetterErrorDescription);
        }

        public void Dispose()
        {
            message.Dispose();
        }
    }
}