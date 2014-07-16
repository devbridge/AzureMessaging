AzureMessaging
==============

An open source component for Azure Service Bus messaging which supports spawning any number of background threads for each message queue, so if processing a message is an IO intensive operation you can double the throughput by simply assigning 2 or more worker threads. Component supports automatic Retries on messages generating errors with Failed messages sent to a Dead Letter Queue when its Retry threshold is reached.

##Instalation
TODO

##Server usage

Usage code:

```csharp
    var queueClientFactory = new QueueClientFactory([ServiceBusConnectionString]);
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
	
    //Start server
    server.Start();

    ...
    
    //Stop server
    server.Dispose();
```
MessageHandlerSettings:

* `NoOfThreads` - number of background threads to handle each message queue
* `NoOfRetries` - number of retries if message handling fails. If number of retries is exceeded then message is moved to Dead Letter Queue. If number of retries is not set then, in case of handling failure, message is not moved to Dead Letter Queue.
* `IntervalBetweenRetries` - interval between retries. Note: actual interval may be different if client and Azure Service Bus times are not synchronized.
* `DuplicateIntervalWithEachRetry` -  indicates if interval should be duplicated between retries. For example IntervalBetweenRetries = TimeSpan.FromSeconds(10) and DuplicateIntervalWithEachRetry = true, so first retry will occur after 10 seconds, second - after 20 seconds, etc.

##Client usage
TODO
