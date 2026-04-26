using System;
using System.Threading;
using System.Threading.Tasks;

namespace HostedServices.Cron.Tests
{
    internal class FakeCronJob : ICronJob
    {
        private readonly Action<DateTime>? _onExecute;

        public string CronExpression { get; }
        public int ExecutionCount { get; private set; }
        public DateTime? LastPlannedExecutionTime { get; private set; }

        public FakeCronJob(string cronExpression = "0 * * * *", Action<DateTime>? onExecute = null)
        {
            CronExpression = cronExpression;
            _onExecute = onExecute;
        }

        public Task ExecuteAsync(DateTime plannedExecutionTime, CancellationToken cancellationToken)
        {
            ExecutionCount++;
            LastPlannedExecutionTime = plannedExecutionTime;
            _onExecute?.Invoke(plannedExecutionTime);
            return Task.CompletedTask;
        }
    }
}
