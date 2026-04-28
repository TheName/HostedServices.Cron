namespace HostedServices.Cron.Tests
{
    internal class FakeCronJob : ICronJob
    {
        private readonly Action<DateTime>? _onExecute;
        private readonly Func<DateTime, CancellationToken, Task>? _onExecuteAsync;

        public string CronExpression { get; }
        public bool RunOnStartup { get; }
        public int ExecutionCount { get; private set; }
        public DateTime? LastPlannedExecutionTime { get; private set; }

        public FakeCronJob(
            string cronExpression = "0 * * * *",
            bool runOnStartup = false,
            Action<DateTime>? onExecute = null,
            Func<DateTime, CancellationToken, Task>? onExecuteAsync = null)
        {
            CronExpression = cronExpression;
            RunOnStartup = runOnStartup;
            _onExecute = onExecute;
            _onExecuteAsync = onExecuteAsync;
        }

        public async Task ExecuteAsync(DateTime plannedExecutionTime, CancellationToken cancellationToken)
        {
            ExecutionCount++;
            LastPlannedExecutionTime = plannedExecutionTime;
            if (_onExecuteAsync != null)
                await _onExecuteAsync(plannedExecutionTime, cancellationToken).ConfigureAwait(false);
            else
                _onExecute?.Invoke(plannedExecutionTime);
        }
    }
}
