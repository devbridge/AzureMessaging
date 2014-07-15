using System;
using System.Threading;
using Common.Logging;
using Devbridge.AzureMessaging.Extensions;
using Devbridge.AzureMessaging.Interfaces;

namespace Devbridge.AzureMessaging
{
    public class AzureMessageHandlerWorker : IDisposable
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(AzureMessageHandlerWorker));

        readonly object msgLock = new object();

        private readonly IAzureMessageHandler messageHandler;
        private Thread bgThread;

        public Action<AzureMessageHandlerWorker, Exception> ErrorHandler { get; set; }

        public string QueueName { get; set; }

        private int status;
        public int Status
        {
            get { return status; }
        }

        private int timesStarted;
        public int TimesStarted
        {
            get { return timesStarted; }
        }

        private DateTime lastMsgProcessed;
        public DateTime LastMsgProcessed
        {
            get { return lastMsgProcessed; }
        }

        private int totalMessagesProcessed;
        public int TotalMessagesProcessed
        {
            get { return totalMessagesProcessed; }
        }

        private int msgNotificationsReceived;
        public int MsgNotificationsReceived
        {
            get { return msgNotificationsReceived; }
        }

        private bool processingMessage;
        private readonly IAzureQueueClientFactory queueClientFactory;

        public AzureMessageHandlerWorker(IAzureQueueClientFactory queueClientFactory, IAzureMessageHandler messageHandler, string queueName, Action<AzureMessageHandlerWorker, Exception> errorHandler)
        {
            this.queueClientFactory = queueClientFactory;
            this.messageHandler = messageHandler;
            QueueName = queueName;
            ErrorHandler = errorHandler;
        }

        public void Start()
        {
            if (Interlocked.CompareExchange(ref status, 0, 0) == WorkerStatus.Started)
            {
                return;
            }

            if (Interlocked.CompareExchange(ref status, 0, 0) == WorkerStatus.Disposed)
            {
                throw new ObjectDisposedException("MQ Host has been disposed");
            }

            if (Interlocked.CompareExchange(ref status, 0, 0) == WorkerStatus.Stopping)
            {
                KillBgThreadIfExists();
            }

            if (Interlocked.CompareExchange(ref status, WorkerStatus.Starting, WorkerStatus.Stopped) == WorkerStatus.Stopped)
            {
                Log.Debug("Starting Azure MQ Handler Worker: {0}...".Fmt(QueueName));

                //Should only be 1 thread past this point
                bgThread = new Thread(Run)
                {
                    Name = "{0}: {1}".Fmt(GetType().Name, QueueName),
                    IsBackground = true,
                };
                bgThread.Start();
            }
        }

        private IAzureQueueClient GetQueueClient(string queueName)
        {
            return queueClientFactory.Create(queueName, true);
        }

        /// <summary>
        /// Runs this instance.
        /// </summary>
        private void Run()
        {
            if (Interlocked.CompareExchange(ref status, WorkerStatus.Started, WorkerStatus.Starting) != WorkerStatus.Starting)
            {
                return;
            }
            timesStarted++;

            try
            {
                var client = GetQueueClient(messageHandler.QueueName);

                while (Interlocked.CompareExchange(ref status, 0, 0) == WorkerStatus.Started)
                {
                    // Long poling, will wait until new message is received or timeout:
                    using (var message = client.Receive())
                    {
                        if (message == null)
                        {
                            continue;
                        }

                        lock (msgLock)
                        {
                            processingMessage = true;
                        }

                        msgNotificationsReceived++;

                        messageHandler.ProcessMessage(message);

                        totalMessagesProcessed++;

                        lastMsgProcessed = DateTime.UtcNow;

                        lock (msgLock)
                        {
                            processingMessage = false;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Ignore handling rare, but expected exceptions from KillBgThreadIfExists()
                if (ex is ThreadInterruptedException || ex is ThreadAbortException)
                {
                    if (!processingMessage)
                    {
                        Log.Warn("Received {0} in Worker: {1}, processingMessage: {2}".Fmt(ex.GetType().Name, QueueName, processingMessage));
                        return;
                    }
                    Log.Warn("Received {0} in Worker: {1}, processingMessage: {2}".Fmt(ex.GetType().Name, QueueName, processingMessage));
                    return;
                }

                Stop();

                if (ErrorHandler != null)
                {
                    ErrorHandler(this, ex);
                }
            }
            finally
            {
                // If it's in an invalid state, Dispose() this worker.
                if (Interlocked.CompareExchange(ref status, WorkerStatus.Stopped, WorkerStatus.Stopping) != WorkerStatus.Stopping)
                {
                    Dispose();
                }
            }
        }

        private void KillBgThreadIfExists()
        {
            try
            {
                if (bgThread != null && bgThread.IsAlive)
                {
                    lock (msgLock)
                    {
                        if (!processingMessage)
                        {
                            Log.Debug("Thread idle (" + bgThread.Name + "), interrupt it.");
                            bgThread.Interrupt();
                        }
                    }

                    //give it a small chance to die gracefully
                    if (!bgThread.Join(500))
                    {
                        //Ideally we shouldn't get here, but lets try our hardest to clean it up
                        Log.Warn("Interrupting previous Background Worker: " + bgThread.Name);
                        bgThread.Interrupt();
                        if (!bgThread.Join(TimeSpan.FromSeconds(3)))
                        {
                            Log.Warn(bgThread.Name + " just wont die, so we're now aborting it...");
                            bgThread.Abort();
                        }
                    }
                }
            }
            finally
            {
                bgThread = null;
                status = WorkerStatus.Stopped;
            }
        }

        public void Dispose()
        {
            if (Interlocked.CompareExchange(ref status, 0, 0) == WorkerStatus.Disposed)
                return;

            Stop();

            if (Interlocked.CompareExchange(ref status, WorkerStatus.Disposed, WorkerStatus.Stopped) != WorkerStatus.Stopped)
                Interlocked.CompareExchange(ref status, WorkerStatus.Disposed, WorkerStatus.Stopping);

            try
            {
                KillBgThreadIfExists();
            }
            catch (Exception ex)
            {
                Log.Error("Error Disposing MessageHandlerWorker for: " + QueueName, ex);
            }
        }

        public void Stop()
        {
            if (Interlocked.CompareExchange(ref status, 0, 0) == WorkerStatus.Disposed)
            {
                return;
            }

            if (Interlocked.CompareExchange(ref status, WorkerStatus.Stopping, WorkerStatus.Started) == WorkerStatus.Started)
            {
                Log.Debug("Stopping MQ Handler Worker: {0}...".Fmt(QueueName));
                if (processingMessage)
                {
                    Thread.Sleep(100);
                }
            }
        }

        public AzureMessageHandlerWorker Clone()
        {
            return new AzureMessageHandlerWorker(queueClientFactory, messageHandler, QueueName, ErrorHandler);
        }

        public IMessageHandlerStats GetStats()
        {
            return new MessageHandlerStats(QueueName,
                TotalMessagesProcessed, 0, 0,
                0, 0, lastMsgProcessed);
        }
    }
}