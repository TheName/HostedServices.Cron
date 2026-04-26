using System;
using System.Threading;
using System.Threading.Tasks;
using Cronos;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HostedServices.Cron
{
    /// <summary>
    /// Abstract base class for a <see cref="BackgroundService"/> that executes a job on a cron schedule.
    /// </summary>
    /// <remarks>
    /// Subclasses must implement <see cref="CronExpression"/>, <see cref="CronJobType"/>, and
    /// <see cref="CronJob"/>. For most cases, use the concrete
    /// <see cref="CronJobHostedService{TCronJob}"/> together with
    /// <see cref="Extensions.ServiceCollectionExtensions.AddCronJobHostedService{TCronJob}(Microsoft.Extensions.DependencyInjection.IServiceCollection)"/>
    /// instead of deriving directly from this class.
    /// </remarks>
    public abstract class CronJobHostedService : BackgroundService
    {
        private readonly ILogger _logger;
        private readonly Lazy<CronExpression> _lazyCronExpression;

        /// <summary>Gets the cron expression string used to schedule executions.</summary>
        protected abstract string CronExpression { get; }

        /// <summary>
        /// Gets the <see cref="Type"/> of the concrete cron job, used for diagnostic logging.
        /// </summary>
        protected abstract Type CronJobType { get; }

        private CronExpression ParsedCronExpression => _lazyCronExpression.Value;

        /// <summary>
        /// Initialises a new instance of <see cref="CronJobHostedService"/>.
        /// </summary>
        /// <param name="logger">The logger used for diagnostic output.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="logger"/> is <see langword="null"/>.
        /// </exception>
        protected CronJobHostedService(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _lazyCronExpression = new Lazy<CronExpression>(() => Cronos.CronExpression.Parse(CronExpression));
        }

        /// <inheritdoc/>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation(
                "CronJob (service) of type {CronJobType} registered",
                CronJobType);

            while (!stoppingToken.IsCancellationRequested)
            {
                var nextOccurence = ParsedCronExpression.GetNextOccurrence(DateTime.UtcNow);
                if (!nextOccurence.HasValue)
                {
                    _logger.LogError(
                        "Could not find next occurrences of the job; cron job type {CronJobType} and expression {CronExpression}",
                        CronJobType,
                        ParsedCronExpression);

                    throw new Exception($"Could not find next occurrences of the job; cron job type {GetType()} and expression {CronExpression}");
                }

                _logger.LogInformation(
                    "CronJob of type {CronJobType} has next occurence at {NextOccurenceTime}",
                    CronJobType,
                    nextOccurence);

                await Task.Delay(nextOccurence.Value.Subtract(DateTime.UtcNow), stoppingToken).ConfigureAwait(false);
                try
                {
                    _logger.LogInformation("Starting CronJob of type {CronJobType}", CronJobType);
                    await CronJob(nextOccurence.Value, stoppingToken).ConfigureAwait(false);
                    _logger.LogInformation("CronJob of type {CronJobType} finished", CronJobType);
                }
                catch (Exception exception)
                {
                    _logger.LogError(
                        exception,
                        "Execution of cron job of type {CronJobType} scheduled at {ScheduleTime} failed",
                        CronJobType,
                        nextOccurence.Value);
                }
            }

            _logger.LogInformation(
                "CronJob (service) of type {CronJobType} has stopped execution. Is cancellation requested? {CancellationRequested}",
                CronJobType,
                stoppingToken.IsCancellationRequested);
        }

        /// <summary>
        /// Performs the work for a single scheduled occurrence.
        /// </summary>
        /// <param name="plannedExecutionTime">The UTC time at which the current execution was planned.</param>
        /// <param name="cancellationToken">A token to observe for application shutdown.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        protected abstract Task CronJob(DateTime plannedExecutionTime, CancellationToken cancellationToken);
    }
}
