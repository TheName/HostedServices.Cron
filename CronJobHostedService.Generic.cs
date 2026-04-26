using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace HostedServices.Cron
{
    /// <summary>
    /// A <see cref="Microsoft.Extensions.Hosting.BackgroundService"/> that executes an <see cref="ICronJob"/> of type
    /// <typeparamref name="TCronJob"/> according to the job's own cron schedule.
    /// </summary>
    /// <typeparam name="TCronJob">
    /// The type of cron job to execute. Must implement <see cref="ICronJob"/>.
    /// </typeparam>
    public class CronJobHostedService<TCronJob> : CronJobHostedService where TCronJob : ICronJob
    {
        private readonly TCronJob _cronJob;

        /// <inheritdoc/>
        protected override string CronExpression => _cronJob.CronExpression;

        /// <inheritdoc/>
        protected override Type CronJobType => typeof(TCronJob);

        /// <summary>
        /// Initialises a new instance of <see cref="CronJobHostedService{TCronJob}"/>.
        /// </summary>
        /// <param name="cronJob">The cron job instance to execute on schedule.</param>
        /// <param name="logger">The logger used for diagnostic output.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="cronJob"/> or <paramref name="logger"/> is <see langword="null"/>.
        /// </exception>
        public CronJobHostedService(TCronJob cronJob, ILogger<CronJobHostedService<TCronJob>> logger) : base(logger)
        {
            _cronJob = cronJob ?? throw new ArgumentNullException(nameof(cronJob));
        }

        /// <inheritdoc/>
        protected override async Task CronJob(DateTime plannedExecutionTime, CancellationToken cancellationToken)
        {
            await _cronJob.ExecuteAsync(plannedExecutionTime, cancellationToken);
        }
    }
}
