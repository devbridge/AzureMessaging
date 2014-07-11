using System;

namespace Devbridge.AzureMessaging.Extensions
{
    public static class IntExtensions
    {
        public static void Times(this int times, Action<int> actionFn)
        {
            for (var i = 0; i < times; i++)
            {
                actionFn(i);
            }
        }
    }
}