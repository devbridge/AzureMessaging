using System;
using System.Diagnostics;
using System.Threading;
using Devbridge.AzureMessaging.Extensions;
using Devbridge.AzureMessaging.InMemory;
using Devbridge.AzureMessaging.Tests.Messages;
using NUnit.Framework;

namespace Devbridge.AzureMessaging.Tests
{
    public class AzureMessageServiceTests
    {
        private const string ConnectionStringName = "ServiceBusConnectionString";

        [Test]
        public void Does_Process_All_Published_Messages()
        {
            var queueClientFactory = new QueueClientFactory(ConnectionStringName);
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

            server.RegisterHandler<GreetTestMessage>(x =>
            {
                handlerCallCount++;

                Trace.WriteLine("GreetTestMessage: " + x.GetBody().Name);

                return null;
            }, settings);

            server.Start();

            client.Publish(new GreetTestMessage { Name = "1" });
            client.Publish(new GreetTestMessage { Name = "2" });
            client.Publish(new GreetTestMessage { Name = "3" });
            client.Publish(new GreetTestMessage { Name = "4" });

            Thread.Sleep(3000);

            Assert.That(server.GetStats().TotalMessagesProcessed, Is.EqualTo(4));
            Assert.That(handlerCallCount, Is.EqualTo(4));

            server.Dispose();
        }

        [Test]
        public void Should_Delay_Message_Processing()
        {
            var queueClientFactory = new QueueClientFactory(ConnectionStringName);
            var server = new AzureMessageService(queueClientFactory);


            var client = server.MessageFactory.CreateMessageQueueClient();
            var callCount = 0;
            var settings = new MessageHandlerSettings
            {
                NoOfThreads = 1,
                NoOfRetries = 1,
                IntervalBetweenRetries = TimeSpan.FromSeconds(40),  //Actual interval may be different if client and Azure Service Bus times are not synchronized.
                DuplicateIntervalWithEachRetry = false
            };

            server.RegisterHandler<DelayTestMessage>(x =>
            {
                callCount++;

                Trace.Write("GreetWorldDelayTestMessage: " + x.GetBody().Name + " " + callCount + " " + DateTime.Now.ToLongTimeString());

                if (callCount == 1)
                {
                    Trace.WriteLine(" (simulated fail)");
                    throw new Exception("Test Exception");
                }

                Trace.WriteLine(" (succeed)");

                return null;
            }, settings);

            server.Start();

            client.Publish(new DelayTestMessage { Name = "Delayed message" });

            Thread.Sleep((int)TimeSpan.FromSeconds(45).TotalMilliseconds);

            Assert.That(server.GetStats().TotalMessagesProcessed, Is.EqualTo(2));
            Assert.That(callCount, Is.EqualTo(2));

            server.Dispose();
        }

        [Test]
        public void Should_Check_Dead_Letter_Queue()
        {
            var queueClientFactory = new QueueClientFactory(ConnectionStringName);
            var server = new AzureMessageService(queueClientFactory);

            var settings = new MessageHandlerSettings
            {
                NoOfThreads = 1,
                NoOfRetries = 1,
                IntervalBetweenRetries = TimeSpan.FromSeconds(1),
                DuplicateIntervalWithEachRetry = true
            };

            server.RegisterHandler<DeadLetterTestMessage>(x =>
            {
                throw new Exception("Simulated fail");
            }, settings);

            server.Start();

            var client = server.MessageFactory.CreateMessageQueueClient();
            client.Publish(new DeadLetterTestMessage { Name = "test" });

            Thread.Sleep(1000);

            var deadMessages = AzureMessageService.GetDeadLetteredMessages<DeadLetterTestMessage>(ConnectionStringName);

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

            server.RegisterHandler<GreetTestMessage>(x =>
            {
                handlerCallCount++;
                Trace.WriteLine("GreetTestMessage: " + x.GetBody().Name);

                return null;
            }, settings);

            server.RegisterHandler<GreetWorldTestMessage>(x =>
            {
                handlerCallCount++;
                Trace.WriteLine("GreetWorldTestMessage: " + x.GetBody().Name);

                return null;
            }, settings);

            server.Start();

            client.Publish(new GreetTestMessage { Name = "1" });
            client.Publish(new GreetTestMessage { Name = "2" });
            client.Publish(new GreetWorldTestMessage { Name = "3" });
            client.Publish(new GreetWorldTestMessage { Name = "4" });

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
}