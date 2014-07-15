using System;
using System.Collections.Generic;

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
}