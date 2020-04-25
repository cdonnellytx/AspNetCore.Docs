using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace BackgroundTasksSample.Services
{
    public interface IBackgroundTaskQueue
    {
        void QueueBackgroundWorkItem(Func<CancellationToken, Task> workItem);

        Task<Func<CancellationToken, Task>> DequeueAsync(CancellationToken cancellationToken);
        Task<bool> HasPendingQueueItemsAsync(CancellationToken cancellationToken);
    }

    public class BackgroundTaskQueue : IBackgroundTaskQueue
    {
        private readonly ILogger<BackgroundTaskQueue> _logger;

        private readonly ConcurrentQueue<Func<CancellationToken, Task>> _workItems = new ConcurrentQueue<Func<CancellationToken, Task>>();
        private readonly SemaphoreSlim _signal = new SemaphoreSlim(5);

        public BackgroundTaskQueue(ILogger<BackgroundTaskQueue> logger)
        {
            _logger = logger;
        }

        public void QueueBackgroundWorkItem(Func<CancellationToken, Task> workItem)
        {
            _logger.LogInformation("Enqueued up a new item");
            if (workItem == null)
            {
                throw new ArgumentNullException(nameof(workItem));
            }

            _workItems.Enqueue(workItem);
            _signal.Release();
        }

        public async Task<Func<CancellationToken, Task>> DequeueAsync(
            CancellationToken cancellationToken)
        {
            await _signal.WaitAsync(cancellationToken);
            _workItems.TryDequeue(out var workItem);

            return workItem;
        }

        public async Task<bool> HasPendingQueueItemsAsync(CancellationToken cancellationToken)
        {
            await _signal.WaitAsync(cancellationToken);
            return !_workItems.IsEmpty;
        }
    }
}
