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
        /// When <see langword="true"/>, the job fires once immediately in the background at host
        /// start-up — before the first scheduled cron tick — then continues on the normal schedule.
        /// </summary>
        /// <remarks>
        /// <para>The start-up run is non-blocking: <c>IHost.StartAsync</c> returns promptly and the
        /// run executes on a background thread.</para>
        /// <para>The first scheduled tick fires only after the start-up run completes, so there is
        /// no concurrent overlap between the two.</para>
        /// <para>If the start-up run throws, the exception is logged at
        /// <see cref="Microsoft.Extensions.Logging.LogLevel.Error"/> and the normal schedule
        /// continues; the host is never crashed.</para>
        /// <para>The run honours application shutdown: if the host stops while the run is in
        /// progress the job's <see cref="System.Threading.CancellationToken"/> is cancelled and
        /// <c>StopAsync</c> waits for the run to finish.</para>
        /// <para>Return <see langword="false"/> (or inherit from <see cref="CronJobBase"/> which
        /// defaults to <see langword="false"/>) to keep the existing schedule-only behaviour.</para>
        /// </remarks>
        bool RunOnStartup { get; }

        /// <summary>
        /// Executes the job asynchronously.
        /// </summary>
        /// <param name="plannedExecutionTime">
        /// The UTC time at which this execution was scheduled according to the cron expression,
        /// or the UTC time of host start-up for a <see cref="RunOnStartup"/> execution.
        /// </param>
        /// <param name="cancellationToken">
        /// A <see cref="CancellationToken"/> that is cancelled when the application is shutting down.
        /// </param>
        /// <returns>A <see cref="Task"/> that represents the asynchronous execution.</returns>
        Task ExecuteAsync(DateTime plannedExecutionTime, CancellationToken cancellationToken);
    }
}
