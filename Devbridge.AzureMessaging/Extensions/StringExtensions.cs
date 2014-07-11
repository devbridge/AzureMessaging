using System;
using Devbridge.AzureMessaging.Interfaces;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Devbridge.AzureMessaging.Extensions
{
    public static class StringExtensions
    {
        public static string Fmt(this string text, params object[] args)
        {
            return string.Format(text, args);
        }

        public static string SerializeToString<T>(this IMessage<T> obj)
        {
            return JsonConvert.SerializeObject(obj);
        }

        public static Message<T> DeserializeToMessage<T>(this string value)
        {
            var message = JsonConvert.DeserializeObject<Message<T>>(value);
            var body = message.Body as JObject;

            if (body != null)
            {
                message.Body = body.ToObject<T>();
            }

            return message;
        }

        /// <summary>
        /// Print string.Format to Console.WriteLine
        /// </summary>
        public static void Print(this string text, params object[] args)
        {
            if (args.Length > 0)
                Console.WriteLine(text, args);
            else
                Console.WriteLine(text);
        }
    }
}