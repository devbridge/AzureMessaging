using System;
using Devbridge.AzureMessaging.Interfaces;

namespace Devbridge.AzureMessaging
{
    public class AzureMessageHandlerFactory<T> : IAzureMessageHandlerFactory
    {
        public Func<IMessage, IMessage> RequestFilter { get; set; }
        public Func<object, object> ResponseFilter { get; set; }

        private readonly IAzureQueueClientFactory queueClientFactory;
        private readonly Func<IMessage<T>, object> processMessageFn;
        private readonly Action<IMessage<T>, Exception> processExceptionFn;
        private readonly MessageHandlerSettings settings;
        public int RetryCount { get; set; }

        public AzureMessageHandlerFactory(IAzureQueueClientFactory queueClientFactory, Func<IMessage<T>, object> processMessageFn, Action<IMessage<T>, Exception> processExceptionFn, MessageHandlerSettings settings)
        {
            this.queueClientFactory = queueClientFactory;
            this.processMessageFn = processMessageFn;
            this.processExceptionFn = processExceptionFn;
            this.settings = settings;
        }

        public IAzureMessageHandler CreateMessageHandler()
        {
            if (RequestFilter == null && ResponseFilter == null)
            {
                return new AzureMessageHandler<T>(queueClientFactory, processMessageFn, processExceptionFn, settings);
            }

            return new AzureMessageHandler<T>(queueClientFactory, msg =>
            {
                if (RequestFilter != null)
                {
                    msg = (IMessage<T>)RequestFilter(msg);
                }

                var result = processMessageFn(msg);

                if (ResponseFilter != null)
                {
                    result = ResponseFilter(result);
                }

                return result;
            }, processExceptionFn, settings);
        }
    }
}