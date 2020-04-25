using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BackgroundTasksSample.Services
{
    public class QueuedHostedService : BackgroundService
    {
        private readonly ILogger<QueuedHostedService> _logger;

        private readonly List<Task> _activeWorkItems = new List<Task>();
        private readonly SemaphoreSlim _signal = new SemaphoreSlim(1);
        private const int ConcurrentProcessingLimit = 10; //TODO: Set this from a config file or something

        public QueuedHostedService(IBackgroundTaskQueue taskQueue,
            ILogger<QueuedHostedService> logger)
        {
            TaskQueue = taskQueue;
            _logger = logger;
        }

        public IBackgroundTaskQueue TaskQueue { get; }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation(
                $"Queued Hosted Service is running.{Environment.NewLine}" +
                $"{Environment.NewLine}Tap W to add a work item to the " +
                $"background queue.{Environment.NewLine}");

            await BackgroundProcessing(stoppingToken);
        }

        private async Task BackgroundProcessing(CancellationToken stoppingToken)
        {
            var queueingTask = Task.Run(async () => await ContinualQueueingToActiveTasks(stoppingToken), stoppingToken);
            var processingTask = Task.Run(async () => await ProcessQueueItemsAsync(stoppingToken), stoppingToken);

            await Task.WhenAny(queueingTask, processingTask);
        }

        private async Task ContinualQueueingToActiveTasks(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Getting into the dequeing loop.");
            while (!stoppingToken.IsCancellationRequested)
            {
                if (!await TaskQueue.HasPendingQueueItemsAsync(stoppingToken))
                {
                    _logger.LogInformation("QUEUE EMPTY. Hit W to queue a task.");
                    await Task.Delay(1000, stoppingToken);
                    continue;
                }
                await _signal.WaitAsync(stoppingToken);
                var toAdd = ConcurrentProcessingLimit - _activeWorkItems.Count;
                _logger.LogInformation("ADDING {toAdd} items to add to the active queue.", toAdd);
                if (toAdd > 0)
                {
                    for (var i = 0; i < toAdd; i++)
                    {
                        _activeWorkItems.Add(Task.Run(async () =>
                        {
                            var workItem = await TaskQueue.DequeueAsync(stoppingToken);
                            if (workItem == null) { return; } //There's a race condition here if we queue things up too fast I don't want to dig into right now
                            await workItem(stoppingToken);
                        }, stoppingToken));
                    }
                }

                _signal.Release();
                await Task.Delay(1000, stoppingToken);
            }
        }

        private async Task ProcessQueueItemsAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var copyOfActiveTasks = _activeWorkItems.ToArray();
                if (!copyOfActiveTasks.Any())
                {
                    _logger.LogInformation("PROCESSOR EMPTY, skipping for 1 second.");
                    await Task.Delay(1000, stoppingToken);
                    continue;
                }
                _logger.LogInformation("PROCESSING a total of {totalTasks}", copyOfActiveTasks.Length);
                var taskToRemove = await Task.WhenAny(copyOfActiveTasks);
                if (taskToRemove.IsCompleted)
                {
                    _logger.LogInformation("REMOVING task.");
                    await _signal.WaitAsync(stoppingToken);
                    _activeWorkItems.Remove(taskToRemove);
                    _signal.Release();
                }
            }
        }

        public override async Task StopAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Queued Hosted Service is stopping.");

            await base.StopAsync(stoppingToken);
        }
    }
}
