using System;

namespace Devbridge.AzureMessaging
{
    public class MessageHandlerSettings
    {
        public int NoOfThreads { get; set; }

        public int NoOfRetries { get; set; }

        public TimeSpan? IntervalBetweenRetries { get; set; }

        public TimeSpan? MaxIntervalBetweenRetries { get; set; }

        public bool DuplicateIntervalWithEachRetry { get; set; }

        public MessageHandlerSettings()
        {
            NoOfThreads = 1;
            NoOfRetries = 1;
        }
    }
}