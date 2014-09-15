AzureMessaging
==============

An open source component for Azure Service Bus messaging which supports spawning any number of background threads for each message queue, so if processing a message is an IO intensive operation you can double the throughput by simply assigning 2 or more worker threads. Component supports automatic Retries on messages generating errors with Failed messages sent to a Dead Letter Queue when its Retry threshold is reached.

##Installation
TODO

##Messages

Client with server comunicates by sending messages. Messages example:

```csharp
public class GreetSampleMessage
{
    public string Text { get; set; }
}

public class GreetWorldSampleMessage
{
    public string Name { get; set; }
    public string Decription { get; set; }
}
```
**Note**: It is important to have unique class names, because queue name is constructed from type name.


##Server Usage

Usage code:

```csharp
var queueClientFactory = new QueueClientFactory(SERVICE_BUS_CONNECTION_STRING_NAME);
var server = new AzureMessageService(queueClientFactory);

//Server settings
var settings = new MessageHandlerSettings
{
    NoOfThreads = 2,
    NoOfRetries = 3,
    IntervalBetweenRetries = TimeSpan.FromSeconds(10),
    DuplicateIntervalWithEachRetry = true
};

//Register handers to handle messages from clients
server.RegisterHandler<GreetSampleMessage>(x =>
{
    var greetSampleMessage = x.GetBody();
    ...
    
    return null;
}, settings);

server.RegisterHandler<GreetWorldSampleMessage>(x =>
{
    var greetWorldSampleMessage = x.GetBody();
    ...
    
    return null;
}, settings);

//Start messages handling
server.Start();

...

//Stop messages handling
server.Stop();

//Free resources
server.Dispose();
```
MessageHandlerSettings:

* `NoOfThreads` - number of background threads to handle each message queue
* `NoOfRetries` - number of retries if message handling fails. If number of retries is exceeded then message is moved to Dead Letter Queue. If number of retries is not set then, in case of handling failure, message is not moved to Dead Letter Queue.
* `IntervalBetweenRetries` - interval between retries. **Note**: actual interval may be different if client and Azure Service Bus times are not synchronized.
* `DuplicateIntervalWithEachRetry` -  indicates if interval should be duplicated between retries. For example IntervalBetweenRetries = TimeSpan.FromSeconds(10) and DuplicateIntervalWithEachRetry = true, so first retry will occur after 10 seconds, second - after 20 seconds, etc.

Get messages from Dead Letter Queue:
```csharp
var deadMessages = AzureMessageService.GetDeadLetteredMessages<GreetSampleMessage>(SERVICE_BUS_CONNECTION_STRING_NAME, messagesCount: 10, deleteAfterReceiving: true);
```

**Note**: Currently handler return result is not processed. In the future we plan to place returned result to queue if result is a class (not null or primitive type).

##Client Usage

Usage code:

```csharp
//Create client
var queueClientFactory = new QueueClientFactory(SERVICE_BUS_CONNECTION_STRING_NAME);
var clientFactory = new AzureMessageClientFactory(queueClientFactory);
var client = clientFactory.CreateMessageQueueClient();

//Send messages
client.Publish(new GreetSampleMessage { Text = "Hello" });
client.Publish(new GreetSampleMessage { Text = "Hello2" });
client.Publish(new GreetWorldSampleMessage { Name = "Greet", Description = "Hellow world" });

```

##Additional Notes

When registering handlers, if database repository is used, need to create LifeTime scope, so that new NHibernate session is created and properly disposed for each message.

```csharp
server.RegisterHandler<QbCreateOrUpdateProduct>(message =>
{
    var body = message.GetBody();
    var productId = body.ProductId;

    using (var scope = BeginLifetimScope())
    {
        scope.Resolve<IProductService>().SaveProduct(productId);
    }

    return null;
}, settings);

```

##Easily Testable

There is also an InMemoryQueueClientFactory available, useful for development & testing.


```csharp
var queueClientFactory = new InMemoryQueueClientFactory();
var server = new AzureMessageService(queueClientFactory);
```

This will process messages without publishing into Azure Message Queue. It is usefull when debugging your own published messages.

##Examples
For more examples look in projects:
* Devbridge.AzureMessaging.Sample.Server
* Devbridge.AzureMessaging.Sample.Client
* Devbridge.AzureMessaging.Tests

To run examples and tests you need to set Azure Service Bus connection string in App.config file:
```xml
<connectionStrings>
    <add name="ServiceBusConnectionString" connectionString="SERVICE_BUS_CONNECTION_STRING" />
</connectionStrings>
```

##License
AzureMessaging is e freely distributable under the terms of an Apache V2 license.

##Authors
Tomas Kirda / [@tkirda](https://twitter.com/tkirda)
<br>
Paulius Grabauskas
