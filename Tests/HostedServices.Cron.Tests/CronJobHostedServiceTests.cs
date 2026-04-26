using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HostedServices.Cron.Tests
{
    public class CronJobHostedServiceTests
    {
        [Fact]
        public void Constructor_WhenCronJobIsNull_ThrowsArgumentNullException()
        {
            var logger = NullLogger<CronJobHostedService<FakeCronJob>>.Instance;

            var ex = Assert.Throws<ArgumentNullException>(
                () => new CronJobHostedService<FakeCronJob>(null!, logger));

            Assert.Equal("cronJob", ex.ParamName);
        }

        [Fact]
        public void Constructor_WhenLoggerIsNull_ThrowsArgumentNullException()
        {
            var cronJob = new FakeCronJob();

            var ex = Assert.Throws<ArgumentNullException>(
                () => new CronJobHostedService<FakeCronJob>(cronJob, null!));

            Assert.Equal("logger", ex.ParamName);
        }

        [Fact]
        public async Task StartAsync_ThenStopAsync_CompletesWithoutException()
        {
            var cronJob = new FakeCronJob();
            var logger = NullLogger<CronJobHostedService<FakeCronJob>>.Instance;
            using var service = new CronJobHostedService<FakeCronJob>(cronJob, logger);

            await service.StartAsync(CancellationToken.None);
            await service.StopAsync(CancellationToken.None);
        }

        [Fact]
        public async Task StopAsync_DoesNotLogErrors()
        {
            var logger = new RecordingLogger<CronJobHostedService<FakeCronJob>>();
            var cronJob = new FakeCronJob();
            using var service = new CronJobHostedService<FakeCronJob>(cronJob, logger);

            await service.StartAsync(CancellationToken.None);
            await service.StopAsync(CancellationToken.None);

            var errors = logger.Entries
                .Where(e => e.Level is LogLevel.Error or LogLevel.Critical)
                .ToList();
            Assert.Empty(errors);
        }

        [Fact]
        public async Task CronJob_WhenThrowsOperationCanceledException_PropagatesWithoutLogging()
        {
            // The protected CronJob method is a pure delegation layer — it has no logging of its
            // own. The OperationCanceledException catch (with the Info log) lives one level up in
            // the ExecuteAsync loop and is guarded by stoppingToken.IsCancellationRequested.
            var cronJob = new FakeCronJob(onExecute: _ => throw new OperationCanceledException());
            var logger = new RecordingLogger<CronJobHostedService<FakeCronJob>>();
            var service = new TestableCronJobHostedService(cronJob, logger);

            await Assert.ThrowsAsync<OperationCanceledException>(
                () => service.InvokeCronJobAsync(DateTime.UtcNow, CancellationToken.None));

            Assert.Empty(logger.Entries);
        }

        [Fact]
        public async Task CronJob_DelegatesToICronJob_ExecuteAsync()
        {
            var capturedTimes = new List<DateTime>();
            var cronJob = new FakeCronJob(onExecute: t => capturedTimes.Add(t));
            var logger = NullLogger<CronJobHostedService<FakeCronJob>>.Instance;
            var service = new TestableCronJobHostedService(cronJob, logger);
            var plannedTime = new DateTime(2024, 6, 1, 12, 0, 0, DateTimeKind.Utc);

            await service.InvokeCronJobAsync(plannedTime, CancellationToken.None);

            Assert.Single(capturedTimes);
            Assert.Equal(plannedTime, capturedTimes[0]);
        }

        [Fact]
        public async Task CronJob_IncrementsCronJobExecutionCount()
        {
            var cronJob = new FakeCronJob();
            var logger = NullLogger<CronJobHostedService<FakeCronJob>>.Instance;
            var service = new TestableCronJobHostedService(cronJob, logger);

            await service.InvokeCronJobAsync(DateTime.UtcNow, CancellationToken.None);
            await service.InvokeCronJobAsync(DateTime.UtcNow, CancellationToken.None);

            Assert.Equal(2, cronJob.ExecutionCount);
        }

        [Fact]
        public async Task CronJob_PropagatesExceptionFromICronJob()
        {
            var cronJob = new FakeCronJob(onExecute: _ => throw new InvalidOperationException("job error"));
            var logger = NullLogger<CronJobHostedService<FakeCronJob>>.Instance;
            var service = new TestableCronJobHostedService(cronJob, logger);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.InvokeCronJobAsync(DateTime.UtcNow, CancellationToken.None));
        }

        /// <summary>Exposes the protected <c>CronJob</c> method for unit testing.</summary>
        private sealed class TestableCronJobHostedService : CronJobHostedService<FakeCronJob>
        {
            public TestableCronJobHostedService(
                FakeCronJob cronJob,
                ILogger<CronJobHostedService<FakeCronJob>> logger)
                : base(cronJob, logger) { }

            public Task InvokeCronJobAsync(DateTime plannedTime, CancellationToken ct)
                => CronJob(plannedTime, ct);
        }
    }
}
