using System;
using Common.Logging;
using Devbridge.AzureMessaging.Extensions;
using Devbridge.AzureMessaging.Interfaces;
using Microsoft.ServiceBus.Messaging;

namespace Devbridge.AzureMessaging
{
    public class AzureMessageHandler<T> : IAzureMessageHandler, IDisposable
    {
        const string RetryCountKey = "RetryCount";

        private static readonly ILog Log = LogManager.GetLogger(typeof(AzureMessageHandler<T>));

        private readonly Func<IMessage<T>, object> processMessageFn;
        private readonly Action<IMessage<T>, Exception> processExceptionFn;
        private readonly int retryCount;
        private readonly MessageHandlerSettings settings;
        private readonly IAzureQueueClientFactory queueClientFactory;

        public int TotalMessagesFailed { get; private set; }

        public string QueueName { get; set; }

        protected int TotalNormalMessagesReceived { get; set; }

        protected DateTime LastMessageProcessed { get; set; }

        public AzureMessageHandler(IAzureQueueClientFactory queueClientFactory, Func<IMessage<T>, object> processMessageFn, Action<IMessage<T>, Exception> processExceptionFn, MessageHandlerSettings settings)
        {
            this.queueClientFactory = queueClientFactory;
            this.processMessageFn = processMessageFn;
            this.processExceptionFn = processExceptionFn;
            this.settings = settings;

            retryCount = settings.NoOfRetries;

            QueueName = typeof(T).QueueName();
        }

        public object ProcessMessage(IAzureMessage message)
        {
            IMessage<T> body = null;
            string messageBody = null;

            object result = null;

            try
            {
                try
                {
                    messageBody = message.GetBody<string>();
                    body = messageBody.DeserializeToMessage<T>();
                }
                catch (Exception e)
                {
                    Log.Error(string.Format("Unable to get message body. {0}: {1}", e.GetType().Name, e.Message), e);
                    throw;
                }

                result = processMessageFn(body);

                message.Complete();

                if (result != null && result.GetType().IsClass)
                {
                    //TODO: in the future add result to out queue
                }
            }
            catch (Exception ex)
            {
                Log.Error(string.Format("Unable process message. {0}: {1}", ex.GetType().Name, ex.Message), ex);

                try
                {
                    TotalMessagesFailed++;

                    var enqueAfter = settings.IntervalBetweenRetries;

                    if (enqueAfter.HasValue)
                    {
                        var messageRetryCount = GetMessageRetryCount(message) + 1;
                        var enqueIn = enqueAfter.Value;

                        if (settings.DuplicateIntervalWithEachRetry)
                        {
                            enqueIn = enqueIn.IncreaseEnqueTime(messageRetryCount);
                        }

                        if (settings.MaxIntervalBetweenRetries.HasValue && enqueIn > settings.MaxIntervalBetweenRetries.Value)
                        {
                            enqueIn = settings.MaxIntervalBetweenRetries.Value;
                        }

                        if (messageRetryCount > retryCount)
                        {
                            message.DeadLetter("RetryCountOver" + retryCount, ex.GetType().Name + ": " + ex.Message);
                        }
                        else
                        {
                            DelayMessageProcessing(messageBody, enqueIn, messageRetryCount, ex);

                            // Remove current message:
                            message.Complete();
                        }
                    }
                    else
                    {
                        // Put back into the queue:
                        message.Abandon();
                    }

                    if (processExceptionFn != null)
                    {
                        processExceptionFn(body, ex);
                    }
                }
                catch (Exception exWhileProcessing)
                {
                    Log.Error("Message exception handler threw an error", exWhileProcessing);
                }
            }

            return result;
        }

        private void DelayMessageProcessing(string messageBody, TimeSpan enqueAfter, int messageRetryCount, Exception exception)
        {
            var brokeredMessage = new BrokeredMessage(messageBody)
            {
                ScheduledEnqueueTimeUtc = DateTime.UtcNow.Add(enqueAfter)
            };

            brokeredMessage.Properties.Add(RetryCountKey, messageRetryCount);
            brokeredMessage.Properties.Add("ExceptionMessage", exception.Message);
            brokeredMessage.Properties.Add("StackTrace", exception.StackTrace);

            var client = queueClientFactory.Create(QueueName);

            client.Send(brokeredMessage);
        }

        private static int GetMessageRetryCount(IAzureMessage message)
        {
            var messageRetryCount = 0;

            if (message.Properties.ContainsKey(RetryCountKey))
            {
                messageRetryCount = ParseInt(message.Properties[RetryCountKey]);
            }

            return messageRetryCount;
        }

        private static int ParseInt(object obj)
        {
            var count = 0;

            if (obj == null)
            {
                return count;
            }

            int.TryParse(obj.ToString(), out count);

            return count;
        }

        public IMessageHandlerStats GetStats()
        {
            return new MessageHandlerStats(typeof(T).Name,
                TotalMessagesProcessed, TotalMessagesFailed, TotalRetries,
                TotalNormalMessagesReceived, TotalPriorityMessagesReceived, LastMessageProcessed);
        }

        protected int TotalPriorityMessagesReceived { get; set; }

        protected int TotalRetries { get; set; }

        protected int TotalMessagesProcessed { get; set; }

        public void Dispose()
        {
            throw new NotImplementedException();
        }
    }
}