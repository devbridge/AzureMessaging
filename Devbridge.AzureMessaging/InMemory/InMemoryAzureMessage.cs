using System.Collections.Generic;
using Devbridge.AzureMessaging.Interfaces;

namespace Devbridge.AzureMessaging.InMemory
{
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
}