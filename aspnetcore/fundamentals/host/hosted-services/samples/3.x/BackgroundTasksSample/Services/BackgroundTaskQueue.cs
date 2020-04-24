#nullable enable
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace BackgroundTasksSample.Services
{
    #region snippet1
    public interface IBackgroundTaskQueue
    {
        void QueueBackgroundWorkItem(QueueItem workItem);

        Task<QueueItem?> DequeueAsync(
            CancellationToken cancellationToken);
    }

    public class BackgroundTaskQueue : IBackgroundTaskQueue
    {
        private ConcurrentQueue<QueueItem> _workItems =
            new ConcurrentQueue<QueueItem>();
        private SemaphoreSlim _signal = new SemaphoreSlim(0);
        private readonly ILogger<BackgroundTaskQueue> logger;

        public BackgroundTaskQueue(ILogger<BackgroundTaskQueue> logger)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void QueueBackgroundWorkItem(
            QueueItem workItem)
        {
            logger.LogTrace("ENTRY QBWI");
            if (workItem == null)
            {
                throw new ArgumentNullException(nameof(workItem));
            }

            logger.LogInformation("Enqueuing {workItem}", workItem);
            _workItems.Enqueue(workItem);
            _signal.Release();
            logger.LogTrace("EXIT  QBWI");
        }

        public async Task<QueueItem?> DequeueAsync(
            CancellationToken cancellationToken)
        {
            logger.LogTrace("ENTRY DequeueAsync");
            await _signal.WaitAsync(cancellationToken);
            _workItems.TryDequeue(out var workItem);

            logger.LogTrace("EXIT  DequeueAsync => {workItem}", workItem);
            return workItem;
        }
    }

    public class QueueItem
    {
        public Guid Id { get; } = Guid.NewGuid();
        public override string ToString() => Id.ToString();
    }

    #endregion
}
