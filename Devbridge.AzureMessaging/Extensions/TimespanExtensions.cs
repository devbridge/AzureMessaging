using System;

namespace Devbridge.AzureMessaging.Extensions
{
    public static class TimespanExtensions
    {
        public static TimeSpan IncreaseEnqueTime(this TimeSpan timeSpan, int retryCount)
        {
            var value = new TimeSpan(timeSpan.Ticks);

            for (var i = 1; i < retryCount; i++)
            {
                value = TimeSpan.FromTicks(value.Ticks * 2);
            }

            return value;
        }
    }
}