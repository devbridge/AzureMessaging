using System;

namespace Devbridge.AzureMessaging.Extensions
{
    public static class TypeExtensions
    {
        public static string QueueName(this Type type)
        {
            return type.Name.ToLower();
        }
    }
}