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
    /// Subclasses must implement <see cref="CronExpression"/> and <see cref="CronJob"/>.
    /// Optionally override <see cref="CronJobType"/> to control the type name used in diagnostic logs,
    /// or override <see cref="RunOnStartup"/> to fire once immediately during host start-up.
    /// For most cases, use the concrete <see cref="CronJobHostedService{TCronJob}"/> together with
    /// <see cref="Extensions.ServiceCollectionExtensions.AddCronJobHostedService{TCronJob}(Microsoft.Extensions.DependencyInjection.IServiceCollection)"/>
    /// instead of deriving directly from this class.
    /// </remarks>
    public abstract class CronJobHostedService : BackgroundService
    {
        private readonly ILogger _logger;
        private readonly TimeProvider _timeProvider;
        private readonly Lazy<CronExpression> _lazyCronExpression;

        /// <summary>Gets the cron expression string used to schedule executions.</summary>
        protected abstract string CronExpression { get; }

        /// <summary>
        /// Gets the <see cref="Type"/> of the cron job, used for diagnostic logging.
        /// Defaults to <see cref="object.GetType"/> of the current instance.
        /// Override to return a more specific type (e.g. <c>typeof(TMyJob)</c>).
        /// </summary>
        protected virtual Type CronJobType => GetType();

        /// <summary>
        /// When <see langword="true"/>, fires the job once at the start of
        /// <see cref="BackgroundService.ExecuteAsync"/> before the first scheduled tick.
        /// Defaults to <see langword="false"/>.
        /// </summary>
        protected virtual bool RunOnStartup => false;

        private CronExpression ParsedCronExpression => _lazyCronExpression.Value;

        /// <summary>
        /// Initialises a new instance of <see cref="CronJobHostedService"/>.
        /// </summary>
        /// <param name="logger">The logger used for diagnostic output.</param>
        /// <param name="timeProvider">
        /// The time provider used for scheduling. Pass <see cref="TimeProvider.System"/> for
        /// real-time behaviour, or a test implementation (e.g. <c>FakeTimeProvider</c>) to
        /// control time in tests.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="logger"/> or <paramref name="timeProvider"/> is <see langword="null"/>.
        /// </exception>
        protected CronJobHostedService(ILogger logger, TimeProvider timeProvider)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
            _lazyCronExpression = new Lazy<CronExpression>(() => ParseCronExpression(CronExpression));
        }

        /// <inheritdoc/>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation(
                "CronJob (service) of type {CronJobType} registered",
                CronJobType);

            if (RunOnStartup)
            {
                // Yield so that StartAsync returns before any job code runs, keeping host
                // start-up non-blocking even when the job's ExecuteAsync is synchronous.
                await Task.Yield();

                var startupTime = _timeProvider.GetUtcNow().UtcDateTime;
                try
                {
                    _logger.LogInformation(
                        "Starting CronJob of type {CronJobType} (startup run)",
                        CronJobType);
                    await CronJob(startupTime, stoppingToken).ConfigureAwait(false);
                    _logger.LogInformation(
                        "CronJob of type {CronJobType} finished (startup run)",
                        CronJobType);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    _logger.LogInformation(
                        "CronJob of type {CronJobType} startup run was cancelled because the application is shutting down",
                        CronJobType);
                }
                catch (Exception exception)
                {
                    _logger.LogError(
                        exception,
                        "CronJob of type {CronJobType} startup run at {StartupTime} failed",
                        CronJobType,
                        startupTime);
                }
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                var now = _timeProvider.GetUtcNow().UtcDateTime;
                var nextOccurrence = ParsedCronExpression.GetNextOccurrence(now);
                if (!nextOccurrence.HasValue)
                {
                    _logger.LogError(
                        "Could not find next occurrence of the job; cron job type {CronJobType} and expression {CronExpression}",
                        CronJobType,
                        ParsedCronExpression);

                    throw new InvalidOperationException(
                        $"Could not find next occurrence of the job; cron job type {CronJobType} and expression {CronExpression}");
                }

                _logger.LogInformation(
                    "CronJob of type {CronJobType} has next occurrence at {NextOccurrenceTime}",
                    CronJobType,
                    nextOccurrence);

                var delay = nextOccurrence.Value - now;
                await DelayAsync(delay, stoppingToken).ConfigureAwait(false);

                try
                {
                    _logger.LogInformation("Starting CronJob of type {CronJobType}", CronJobType);
                    await CronJob(nextOccurrence.Value, stoppingToken).ConfigureAwait(false);
                    _logger.LogInformation("CronJob of type {CronJobType} finished", CronJobType);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    _logger.LogInformation(
                        "CronJob of type {CronJobType} was cancelled because the application is shutting down",
                        CronJobType);
                }
                catch (Exception exception)
                {
                    _logger.LogError(
                        exception,
                        "Execution of cron job of type {CronJobType} scheduled at {ScheduleTime} failed",
                        CronJobType,
                        nextOccurrence.Value);
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

        private Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
        {
            if (delay <= TimeSpan.Zero)
                return Task.CompletedTask;

            return _timeProvider.Delay(delay, cancellationToken);
        }

        // Detects seconds-precision (6-field) vs standard (5-field) from the expression itself.
        private static CronExpression ParseCronExpression(string expression)
        {
            var fieldCount = expression.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Length;
            var format = fieldCount == 6 ? CronFormat.IncludeSeconds : CronFormat.Standard;
            return Cronos.CronExpression.Parse(expression, format);
        }
    }
}
