using System;
using System.Configuration;
using System.Diagnostics;
using System.Threading;
using Devbridge.AzureMessaging.Extensions;
using Devbridge.AzureMessaging.InMemory;
using NUnit.Framework;

namespace Devbridge.AzureMessaging.Tests
{
    public class AzureMessageServiceTests
    {
        private readonly string ConnectionString = ConfigurationManager.AppSettings["ServiceBusConnectionString"];

        [Test]
        public void Does_Process_All_Published_Messages()
        {
            var queueClientFactory = new QueueClientFactory(ConnectionString);
            var server = new AzureMessageService(queueClientFactory);


            var client = server.MessageFactory.CreateMessageQueueClient();
            var handlerCallCount = 0;
            var settings = new MessageHandlerSettings
            {
                NoOfThreads = 1,
                NoOfRetries = 1,
                IntervalBetweenRetries = TimeSpan.FromSeconds(10),
                DuplicateIntervalWithEachRetry = true
            };

            server.RegisterHandler<Greet>(x =>
            {
                handlerCallCount++;

                Trace.WriteLine("Greet: " + x.GetBody().Name);
                return x.GetBody().Name;
            }, settings);

            server.Start();

            client.Publish(new Greet { Name = "1" });
            client.Publish(new Greet { Name = "2" });
            client.Publish(new Greet { Name = "3" });
            client.Publish(new Greet { Name = "4" });

            Thread.Sleep(3000);

            Assert.That(server.GetStats().TotalMessagesProcessed, Is.EqualTo(4));
            Assert.That(handlerCallCount, Is.EqualTo(4));

            server.Dispose();
        }

        [Test]
        [Ignore("Run manually only")]
        public void Should_Delay_Message_Processing()
        {
            var queueClientFactory = new QueueClientFactory(ConnectionString);
            var server = new AzureMessageService(queueClientFactory);


            var client = server.MessageFactory.CreateMessageQueueClient();
            var callCount = 0;
            var settings = new MessageHandlerSettings
            {
                NoOfThreads = 1,
                NoOfRetries = 1,
                IntervalBetweenRetries = TimeSpan.FromSeconds(45),
                DuplicateIntervalWithEachRetry = false
            };

            server.RegisterHandler<Greet>(x =>
            {
                callCount++;

                Trace.Write("Greet: " + x.GetBody().Name + " " + callCount + " " + DateTime.Now.ToLongTimeString());

                if (callCount == 1)
                {
                    Trace.WriteLine(" (simulated fail)");
                    throw new Exception("Test Exception");
                }

                Trace.WriteLine(" (succeed)");

                return x.GetBody().Name;
            }, settings);

            server.Start();

            client.Publish(new Greet { Name = "Delayed message" });

            Thread.Sleep((int)TimeSpan.FromSeconds(50).TotalMilliseconds);

            Assert.That(server.GetStats().TotalMessagesProcessed, Is.EqualTo(2));
            Assert.That(callCount, Is.EqualTo(2));

            server.Dispose();
        }

        [Test]
        [Ignore("Experimental Feature")]
        public void Should_Check_Dead_Letter_Queue()
        {
            var queueClientFactory = new QueueClientFactory(ConnectionString);
            var server = new AzureMessageService(queueClientFactory);

            server.Start();

            var deadMessages = server.GetDeadLetteredMessages<Greet>(ConnectionString);

            Assert.That(deadMessages.Count, Is.GreaterThan(0));

            server.Dispose();
        }

        [Test]
        public void Does_Process_All_Published_Messages_InMemory()
        {
            var queueClientFactory = new InMemoryQueueClientFactory();
            var server = new AzureMessageService(queueClientFactory);
            var client = server.MessageFactory.CreateMessageQueueClient();
            var handlerCallCount = 0;
            var settings = new MessageHandlerSettings();

            server.RegisterHandler<Greet>(x =>
            {
                handlerCallCount++;
                Trace.WriteLine("Greet: " + x.GetBody().Name);
                return x.GetBody().Name;
            }, settings);

            server.RegisterHandler<GreetWorld>(x =>
            {
                handlerCallCount++;
                Trace.WriteLine("World: " + x.GetBody().Name);
                return x.GetBody().Name;
            }, settings);

            server.Start();

            client.Publish(new Greet { Name = "1" });
            client.Publish(new Greet { Name = "2" });
            client.Publish(new GreetWorld { Name = "3" });
            client.Publish(new GreetWorld { Name = "4" });

            Thread.Sleep(3000);

            Assert.That(server.GetStats().TotalMessagesProcessed, Is.EqualTo(4));
            Assert.That(handlerCallCount, Is.EqualTo(4));

            server.Dispose();
        }

        [Test]
        public void Duplicate_Time_When_Retrying()
        {
            var timeSpan = TimeSpan.FromSeconds(10);

            var newTime = timeSpan.IncreaseEnqueTime(1);
            Assert.That(newTime.TotalSeconds, Is.EqualTo(10D));

            newTime = timeSpan.IncreaseEnqueTime(2);
            Assert.That(newTime.TotalSeconds, Is.EqualTo(20D));

            newTime = timeSpan.IncreaseEnqueTime(3);
            Assert.That(newTime.TotalSeconds, Is.EqualTo(40D));

            newTime = timeSpan.IncreaseEnqueTime(4);
            Assert.That(newTime.TotalSeconds, Is.EqualTo(80D));

            newTime = timeSpan.IncreaseEnqueTime(5);
            Assert.That(newTime.TotalSeconds, Is.EqualTo(160D));
        }
    }

    public class Greet
    {
        public string Name { get; set; }
    }

    public class GreetWorld
    {
        public string Name { get; set; }
    }
}