using System;

namespace Devbridge.AzureMessaging.Interfaces
{
    public interface IMessage
    {
        DateTime CreatedDate { get; }

        long Priority { get; set; }

        int RetryAttempts { get; set; }

        Guid? ReplyId { get; set; }

        string ReplyTo { get; set; }

        MessageError Error { get; set; }

        object Body { get; set; }
    }

    public interface IMessage<T>
        : IMessage
    {
        T GetBody();
    }
}