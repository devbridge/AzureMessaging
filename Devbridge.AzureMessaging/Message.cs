using System;
using Devbridge.AzureMessaging.Interfaces;

namespace Devbridge.AzureMessaging
{
    /// <summary>
    /// Basic implementation of IMessage[T]
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class Message<T>
        : IMessage<T>
    {
        public Guid Id { get; set; }
        public DateTime CreatedDate { get; set; }
        public long Priority { get; set; }
        public int RetryAttempts { get; set; }
        public Guid? ReplyId { get; set; }
        public string ReplyTo { get; set; }
        public MessageError Error { get; set; }
        public object Body { get; set; }

        public Message()
        {
            Id = Guid.NewGuid();
            CreatedDate = DateTime.UtcNow;
        }

        public Message(T body)
            : this()
        {
            Body = body;
        }

        public static IMessage<T> Create(T body)
        {
            return new Message<T>(body);
        }

        public T GetBody()
        {
            return (T) Body;
        }

        public override string ToString()
        {
            return string.Format("CreatedDate={0}, Id={1}, Type={2}, Retry={3}",
                CreatedDate,
                Id.ToString("N"),
                typeof(T).Name,
                RetryAttempts);
        }

    }

    public class MessageError
    {
        public string ErrorCode { get; set; }

        public string Message { get; set; }

        public string StackTrace { get; set; }
    }
}