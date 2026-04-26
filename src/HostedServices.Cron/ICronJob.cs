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
        /// <para>
        /// Standard 5-field format is supported (e.g. <c>"0 * * * *"</c> runs at the start of
        /// every hour). For sub-minute precision, a 6-field expression with a leading seconds
        /// field is also accepted (e.g. <c>"*/30 * * * * *"</c> runs every 30 seconds).
        /// The format is detected automatically from the number of fields.
        /// </para>
        /// <para>Expressions are parsed by the <c>Cronos</c> library.</para>
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
