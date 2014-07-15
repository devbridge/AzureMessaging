using System.Collections.Generic;
using Devbridge.AzureMessaging.Interfaces;
using Microsoft.ServiceBus.Messaging;

namespace Devbridge.AzureMessaging
{
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