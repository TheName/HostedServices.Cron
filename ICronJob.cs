using System;
using System.Threading;
using System.Threading.Tasks;

namespace HostedServices.Cron
{
    /// <summary>
    /// Defines a cron-scheduled job that can be executed by <see cref="CronJobHostedService{TCronJob}"/>.
    /// </summary>
    public interface ICronJob
    {
        /// <summary>
        /// Gets the cron expression that defines the schedule on which this job runs.
        /// </summary>
        /// <remarks>
        /// Standard 5-field cron expression format is supported
        /// (e.g. <c>"0 * * * *"</c> runs at the start of every hour).
        /// The expression is parsed by the <c>Cronos</c> library.
        /// </remarks>
        string CronExpression { get; }

        /// <summary>
        /// Executes the job asynchronously.
        /// </summary>
        /// <param name="plannedExecutionTime">
        /// The UTC time at which this execution was scheduled according to the cron expression.
        /// </param>
        /// <param name="cancellationToken">
        /// A <see cref="CancellationToken"/> that is cancelled when the application is shutting down.
        /// </param>
        /// <returns>A <see cref="Task"/> that represents the asynchronous execution.</returns>
        Task ExecuteAsync(DateTime plannedExecutionTime, CancellationToken cancellationToken);
    }
}
