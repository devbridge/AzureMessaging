using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading;
using Common.Logging;
using Devbridge.AzureMessaging.Extensions;
using Devbridge.AzureMessaging.Interfaces;
using Microsoft.ServiceBus.Messaging;

namespace Devbridge.AzureMessaging
{
    public class AzureMessageService : IMessageService
    {
        private static readonly ILog Log = LogManager.GetCurrentClassLogger();

        private readonly IAzureQueueClientFactory queueClientFactory;

        public int RetryCount { get; protected set; }

        //Stats
        private long timesStarted;
        private long noOfErrors;
        private int noOfContinuousErrors;
        private int status;

        private Thread bgThread; //Subscription controller thread
        private long bgThreadCount;
        public long BgThreadCount
        {
            get { return Interlocked.CompareExchange(ref bgThreadCount, 0, 0); }
        }

        private readonly Dictionary<Type, IAzureMessageHandlerFactory> handlerMap = new Dictionary<Type, IAzureMessageHandlerFactory>();

        private readonly Dictionary<Type, int> handlerThreadCountMap = new Dictionary<Type, int>();

        private AzureMessageHandlerWorker[] workers;
        private Dictionary<string, int[]> queueWorkerIndexMap;

        /// <summary>
        /// Execute global transformation or custom logic before a request is processed.
        /// Must be thread-safe.
        /// </summary>
        public Func<IMessage, IMessage> RequestFilter { get; set; }

        /// <summary>
        /// Execute global transformation or custom logic on the response.
        /// Must be thread-safe.
        /// </summary>
        public Func<object, object> ResponseFilter { get; set; }

        /// <summary>
        /// Execute global error handler logic. Must be thread-safe.
        /// </summary>
        public Action<Exception> ErrorHandler { get; set; }

        public IMessageFactory MessageFactory { get; private set; }

        public AzureMessageService(IAzureQueueClientFactory queueClientFactory)
        {
            this.queueClientFactory = queueClientFactory;

            MessageFactory = new AzureMessageClientFactory(queueClientFactory);
        }

        public void Dispose()
        {
            if (Interlocked.CompareExchange(ref status, 0, 0) == WorkerStatus.Disposed)
            {
                return;
            }

            Stop();

            if (Interlocked.CompareExchange(ref status, WorkerStatus.Disposed, WorkerStatus.Stopped) != WorkerStatus.Stopped)
            {
                Interlocked.CompareExchange(ref status, WorkerStatus.Disposed, WorkerStatus.Stopping);
            }

            try
            {
                DisposeWorkerThreads();
            }
            catch (Exception ex)
            {
                Log.Error("Error DisposeWorkerThreads(): ", ex);
            }

            try
            {
                // Give it a small chance to die gracefully:
                Thread.Sleep(100);

                KillBgThreadIfExists();
            }
            catch (Exception ex)
            {
                if (ErrorHandler != null)
                {
                    ErrorHandler(ex);
                }
            }
        }

        private void DisposeWorkerThreads()
        {
            Log.Debug("Disposing all Azure MQ Server worker threads...");
            if (workers != null)
            {
                Array.ForEach(workers, x => x.Dispose());
            }
        }

        void WorkerErrorHandler(AzureMessageHandlerWorker source, Exception ex)
        {
            Log.Error("Received exception in Worker: " + source.QueueName, ex);

            for (var i = 0; i < workers.Length; i++)
            {
                var worker = workers[i];
                if (worker == source)
                {
                    Log.Debug("Starting new {0} Worker at index {1}...".Fmt(source.QueueName, i));
                    workers[i] = source.Clone();
                    workers[i].Start();
                    worker.Dispose();
                    return;
                }
            }
        }

        public void RegisterHandler<T>(Func<IMessage<T>, object> processMessageFn, MessageHandlerSettings settings)
        {
            RegisterHandler(processMessageFn, null, settings);
        }

        public void RegisterHandler<T>(Func<IMessage<T>, object> processMessageFn)
        {
            RegisterHandler(processMessageFn, null);
        }

        public void RegisterHandler<T>(Func<IMessage<T>, object> processMessageFn, Action<IMessage<T>, Exception> processExceptionEx, MessageHandlerSettings settings = null)
        {
            var type = typeof(T);

            if (handlerMap.ContainsKey(type))
            {
                throw new ArgumentException("Message handler has already been registered for type: " + type.Name);
            }

            RegisteredTypes.Add(type);

            if (settings == null)
            {
                settings = new MessageHandlerSettings();
            }

            handlerMap[type] = CreateMessageHandlerFactory(processMessageFn, processExceptionEx, settings);
            handlerThreadCountMap[type] = settings.NoOfThreads;
        }

        protected IAzureMessageHandlerFactory CreateMessageHandlerFactory<T>(Func<IMessage<T>, object> processMessageFn, Action<IMessage<T>, Exception> processExceptionEx, MessageHandlerSettings settings)
        {
            return new AzureMessageHandlerFactory<T>(queueClientFactory, processMessageFn, processExceptionEx, settings)
            {
                RequestFilter = RequestFilter,
                ResponseFilter = ResponseFilter,
                RetryCount = RetryCount,
            };
        }

        public IMessageHandlerStats GetStats()
        {
            lock (workers)
            {
                var total = new MessageHandlerStats("All Handlers");
                workers.ToList().ForEach(x => total.Add(x.GetStats()));
                return total;
            }
        }

        public List<Type> RegisteredTypes
        {
            get { return handlerMap.Keys.ToList(); }
        }

        public string GetStatus()
        {
            switch (Interlocked.CompareExchange(ref status, 0, 0))
            {
                case WorkerStatus.Disposed:
                    return "Disposed";
                case WorkerStatus.Stopped:
                    return "Stopped";
                case WorkerStatus.Stopping:
                    return "Stopping";
                case WorkerStatus.Starting:
                    return "Starting";
                case WorkerStatus.Started:
                    return "Started";
            }
            return null;
        }

        public string GetStatsDescription()
        {
            lock (workers)
            {
                var sb = new StringBuilder("#MQ SERVER STATS:\n");
                sb.AppendLine("===============");
                sb.AppendLine("Current Status: " + GetStatus());
                sb.AppendLine("Listening On: " + string.Join(", ", workers.ToList().ConvertAll(x => x.QueueName).ToArray()));
                sb.AppendLine("Times Started: " + Interlocked.CompareExchange(ref timesStarted, 0, 0));
                sb.AppendLine("Num of Errors: " + Interlocked.CompareExchange(ref noOfErrors, 0, 0));
                sb.AppendLine("Num of Continuous Errors: " + Interlocked.CompareExchange(ref noOfContinuousErrors, 0, 0));
                sb.AppendLine("===============");
                foreach (var worker in workers)
                {
                    sb.AppendLine(worker.GetStats().ToString());
                    sb.AppendLine("---------------\n");
                }
                return sb.ToString();
            }
        }

        public void Init()
        {
            if (workers == null)
            {
                var workerBuilder = new List<AzureMessageHandlerWorker>();

                foreach (var entry in handlerMap)
                {
                    var msgType = entry.Key;
                    var handlerFactory = entry.Value;
                    var queueName = msgType.QueueName();

                    var noOfThreads = handlerThreadCountMap[msgType];

                    noOfThreads.Times(i =>
                        workerBuilder.Add(new AzureMessageHandlerWorker(
                            queueClientFactory,
                            handlerFactory.CreateMessageHandler(),
                            queueName,
                            WorkerErrorHandler)));
                }

                workers = workerBuilder.ToArray();

                queueWorkerIndexMap = new Dictionary<string, int[]>();
                for (var i = 0; i < workers.Length; i++)
                {
                    var worker = workers[i];

                    int[] workerIds;
                    if (!queueWorkerIndexMap.TryGetValue(worker.QueueName, out workerIds))
                    {
                        queueWorkerIndexMap[worker.QueueName] = new[] { i };
                    }
                    else
                    {
                        workerIds = new List<int>(workerIds) { i }.ToArray();
                        queueWorkerIndexMap[worker.QueueName] = workerIds;
                    }
                }
            }
        }

        public void Start()
        {
            if (Interlocked.CompareExchange(ref status, 0, 0) == WorkerStatus.Started)
            {
                //Start any stopped worker threads
                StartWorkerThreads();
                return;
            }

            if (Interlocked.CompareExchange(ref status, 0, 0) == WorkerStatus.Disposed)
            {
                throw new ObjectDisposedException("MQ Host has been disposed");
            }

            //Only 1 thread allowed past
            if (Interlocked.CompareExchange(ref status, WorkerStatus.Starting, WorkerStatus.Stopped) == WorkerStatus.Stopped) //Should only be 1 thread past this point
            {
                try
                {
                    Init();

                    if (workers == null || workers.Length == 0)
                    {
                        Log.Warn("Cannot start a MQ Server with no Message Handlers registered, ignoring.");
                        Interlocked.CompareExchange(ref status, WorkerStatus.Stopped, WorkerStatus.Starting);
                        return;
                    }

                    SleepBackOffMultiplier(Interlocked.CompareExchange(ref noOfContinuousErrors, 0, 0));

                    StartWorkerThreads();

                    // Don't kill us if we're the thread that's retrying to Start() after a failure.
                    if (bgThread != Thread.CurrentThread)
                    {
                        KillBgThreadIfExists();

                        bgThread = new Thread(RunLoop)
                        {
                            IsBackground = true,
                            Name = "Azure MQ Server " + Interlocked.Increment(ref bgThreadCount)
                        };
                        bgThread.Start();
                        Log.Debug("Started Background Thread: " + bgThread.Name);
                    }
                    else
                    {
                        Log.Debug("Retrying RunLoop() on Thread: " + bgThread.Name);
                        RunLoop();
                    }
                }
                catch (Exception ex)
                {
                    ex.Message.Print();
                    if (ErrorHandler != null) ErrorHandler(ex);
                }
            }
        }

        private void RunLoop()
        {
            if (Interlocked.CompareExchange(ref status, WorkerStatus.Started, WorkerStatus.Starting) != WorkerStatus.Starting)
            {
                return;
            }
            Interlocked.Increment(ref timesStarted);
        }

        private void KillBgThreadIfExists()
        {
            if (bgThread != null && bgThread.IsAlive)
            {
                //give it a small chance to die gracefully
                if (!bgThread.Join(500))
                {
                    //Ideally we shouldn't get here, but lets try our hardest to clean it up
                    Log.Warn("Interrupting previous Background Thread: " + bgThread.Name);
                    bgThread.Interrupt();
                    if (!bgThread.Join(TimeSpan.FromSeconds(3)))
                    {
                        Log.Warn(bgThread.Name + " just wont die, so we're now aborting it...");
                        bgThread.Abort();
                    }
                }
                bgThread = null;
            }
        }

        readonly Random rand = new Random(Environment.TickCount);

        private void SleepBackOffMultiplier(int continuousErrorsCount)
        {
            if (continuousErrorsCount == 0)
            {
                return;
            }
            const int MaxSleepMs = 60 * 1000;

            //exponential/random retry back-off.
            var nextTry = Math.Min(
                rand.Next((int)Math.Pow(continuousErrorsCount, 3), (int)Math.Pow(continuousErrorsCount + 1, 3) + 1),
                MaxSleepMs);

            Log.Debug("Sleeping for {0}ms after {1} continuous errors".Fmt(nextTry, continuousErrorsCount));

            Thread.Sleep(nextTry);
        }

        public void Stop()
        {
            if (Interlocked.CompareExchange(ref status, 0, 0) == WorkerStatus.Disposed)
            {
                throw new ObjectDisposedException("MQ Host has been disposed");
            }

            if (Interlocked.CompareExchange(ref status, WorkerStatus.Stopping, WorkerStatus.Started) == WorkerStatus.Started)
            {
                Log.Debug("Stopping MQ Host...");
            }
        }

        public void StartWorkerThreads()
        {
            Log.Debug("Starting all Azure MQ Server worker threads...");
            Array.ForEach(workers, x => x.Start());
        }

        public static List<IMessage<T>> GetDeadLetteredMessages<T>(string connectionStringName, int messagesCount = 10, bool deleteAfterReceiving = true)
        {
            var connectionStringSettings = ConfigurationManager.ConnectionStrings[connectionStringName];
            if (connectionStringSettings == null)
            {
                throw new ArgumentException("Invalid connection string name has been supplied", "connectionStringName");
            }
            var messagingFactory = MessagingFactory.CreateFromConnectionString(connectionStringSettings.ConnectionString);

            var queueName = typeof(T).QueueName();
            var receiveMode = deleteAfterReceiving ? ReceiveMode.ReceiveAndDelete : ReceiveMode.PeekLock;
            var queueClient = messagingFactory.CreateQueueClient(QueueClient.FormatDeadLetterPath(queueName), receiveMode);

            var items = new List<IMessage<T>>();

            var brokeredMessages = queueClient.ReceiveBatch(messagesCount);

            if (brokeredMessages == null)
            {
                return items;
            }

            foreach (var brokeredMessage in brokeredMessages)
            {
                if (!deleteAfterReceiving)
                {
                    brokeredMessage.Abandon(); //Unlock message in queue
                }

                var messageBody = brokeredMessage.GetBody<string>();
                var message = messageBody.DeserializeToMessage<T>();

                message.RetryAttempts = ParseInt(brokeredMessage.Properties["RetryCount"]);
                message.Error = new MessageError
                {
                    ErrorCode = GetValue(brokeredMessage.Properties, "ErrorCode"),
                    Message = GetValue(brokeredMessage.Properties, "ErrorMessage"),
                    StackTrace = GetValue(brokeredMessage.Properties, "StackTrace")
                };

                items.Add(message);
            }

            return items;
        }

        private static string GetValue(IDictionary<string, object> dictionary, string key)
        {
            object value;

            return dictionary.TryGetValue(key, out value) ? value as string : null;
        }

        private static int ParseInt(object obj)
        {
            var count = 0;

            if (obj == null)
            {
                return count;
            }

            int.TryParse(obj.ToString(), out count);

            return count;
        }
    }
}