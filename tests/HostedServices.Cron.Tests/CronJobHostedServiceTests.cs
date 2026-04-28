using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace HostedServices.Cron.Tests
{
    public class CronJobHostedServiceTests
    {
        // ------------------------------------------------------------------ constructor guards

        [Fact]
        public void Constructor_WhenCronJobIsNull_ThrowsArgumentNullException()
        {
            var logger = NullLogger<CronJobHostedService<FakeCronJob>>.Instance;

            var ex = Assert.Throws<ArgumentNullException>(
                () => new CronJobHostedService<FakeCronJob>(null!, logger, TimeProvider.System));

            Assert.Equal("cronJob", ex.ParamName);
        }

        [Fact]
        public void Constructor_WhenLoggerIsNull_ThrowsArgumentNullException()
        {
            var cronJob = new FakeCronJob();

            var ex = Assert.Throws<ArgumentNullException>(
                () => new CronJobHostedService<FakeCronJob>(cronJob, null!, TimeProvider.System));

            Assert.Equal("logger", ex.ParamName);
        }

        [Fact]
        public void Constructor_WhenTimeProviderIsNull_ThrowsArgumentNullException()
        {
            var cronJob = new FakeCronJob();
            var logger = NullLogger<CronJobHostedService<FakeCronJob>>.Instance;

            var ex = Assert.Throws<ArgumentNullException>(
                () => new CronJobHostedService<FakeCronJob>(cronJob, logger, null!));

            Assert.Equal("timeProvider", ex.ParamName);
        }

        // ------------------------------------------------------------------ lifecycle

        [Fact]
        public async Task StartAsync_ThenStopAsync_CompletesWithoutException()
        {
            var cronJob = new FakeCronJob();
            var logger = NullLogger<CronJobHostedService<FakeCronJob>>.Instance;
            using var service = new CronJobHostedService<FakeCronJob>(cronJob, logger, TimeProvider.System);

            await service.StartAsync(CancellationToken.None);
            await service.StopAsync(CancellationToken.None);
        }

        [Fact]
        public async Task StopAsync_DoesNotLogErrors()
        {
            var logger = new RecordingLogger<CronJobHostedService<FakeCronJob>>();
            var cronJob = new FakeCronJob();
            using var service = new CronJobHostedService<FakeCronJob>(cronJob, logger, TimeProvider.System);

            await service.StartAsync(CancellationToken.None);
            await service.StopAsync(CancellationToken.None);

            var errors = logger.Entries
                .Where(e => e.Level is LogLevel.Error or LogLevel.Critical)
                .ToList();
            Assert.Empty(errors);
        }

        // ------------------------------------------------------------------ CronJob delegation

        [Fact]
        public async Task CronJob_DelegatesToICronJob_ExecuteAsync()
        {
            var capturedTimes = new List<DateTime>();
            var cronJob = new FakeCronJob(onExecute: t => capturedTimes.Add(t));
            var logger = NullLogger<CronJobHostedService<FakeCronJob>>.Instance;
            var service = new TestableCronJobHostedService(cronJob, logger, TimeProvider.System);
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
            var service = new TestableCronJobHostedService(cronJob, logger, TimeProvider.System);

            await service.InvokeCronJobAsync(DateTime.UtcNow, CancellationToken.None);
            await service.InvokeCronJobAsync(DateTime.UtcNow, CancellationToken.None);

            Assert.Equal(2, cronJob.ExecutionCount);
        }

        [Fact]
        public async Task CronJob_PropagatesExceptionFromICronJob()
        {
            var cronJob = new FakeCronJob(onExecute: _ => throw new InvalidOperationException("job error"));
            var logger = NullLogger<CronJobHostedService<FakeCronJob>>.Instance;
            var service = new TestableCronJobHostedService(cronJob, logger, TimeProvider.System);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.InvokeCronJobAsync(DateTime.UtcNow, CancellationToken.None));
        }

        [Fact]
        public async Task CronJob_WhenThrowsOperationCanceledException_PropagatesWithoutLogging()
        {
            // The protected CronJob method is a pure delegation layer — it has no logging of its
            // own. The OperationCanceledException catch (with the Info log) lives one level up in
            // the ExecuteAsync loop and is guarded by stoppingToken.IsCancellationRequested.
            var cronJob = new FakeCronJob(onExecute: _ => throw new OperationCanceledException());
            var logger = new RecordingLogger<CronJobHostedService<FakeCronJob>>();
            var service = new TestableCronJobHostedService(cronJob, logger, TimeProvider.System);

            await Assert.ThrowsAsync<OperationCanceledException>(
                () => service.InvokeCronJobAsync(DateTime.UtcNow, CancellationToken.None));

            Assert.Empty(logger.Entries);
        }

        // ------------------------------------------------------------------ scheduling integration (FakeTimeProvider)

        [Fact]
        public async Task ExecuteAsync_WhenTimeAdvancesToNextOccurrence_ExecutesJob()
        {
            var startTime = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
            var fakeTime = new FakeTimeProvider(startTime);
            var jobExecuted = new TaskCompletionSource<DateTime>();
            var cronJob = new FakeCronJob("0 * * * *", onExecute: t => jobExecuted.TrySetResult(t));
            var logger = NullLogger<CronJobHostedService<FakeCronJob>>.Instance;
            using var service = new CronJobHostedService<FakeCronJob>(cronJob, logger, fakeTime);

            await service.StartAsync(CancellationToken.None);
            fakeTime.Advance(TimeSpan.FromHours(1)); // jump to 01:00:00 — the first occurrence

            var executedAt = await jobExecuted.Task.WaitAsync(TimeSpan.FromSeconds(5), TimeProvider.System);

            Assert.Equal(new DateTime(2024, 1, 1, 1, 0, 0, DateTimeKind.Utc), executedAt);

            await service.StopAsync(CancellationToken.None);
        }

        [Fact]
        public async Task ExecuteAsync_WithSecondsExpression_ExecutesJobOnSecondBoundary()
        {
            var startTime = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
            var fakeTime = new FakeTimeProvider(startTime);
            var jobExecuted = new TaskCompletionSource<DateTime>();
            var cronJob = new FakeCronJob("*/30 * * * * *", onExecute: t => jobExecuted.TrySetResult(t));
            var logger = NullLogger<CronJobHostedService<FakeCronJob>>.Instance;
            using var service = new CronJobHostedService<FakeCronJob>(cronJob, logger, fakeTime);

            await service.StartAsync(CancellationToken.None);
            fakeTime.Advance(TimeSpan.FromSeconds(30)); // jump to 00:00:30

            var executedAt = await jobExecuted.Task.WaitAsync(TimeSpan.FromSeconds(5), TimeProvider.System);

            Assert.Equal(new DateTime(2024, 1, 1, 0, 0, 30, DateTimeKind.Utc), executedAt);

            await service.StopAsync(CancellationToken.None);
        }

        [Fact]
        public async Task ExecuteAsync_WhenJobThrows_ContinuesAndExecutesOnNextOccurrence()
        {
            var startTime = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
            var fakeTime = new FakeTimeProvider(startTime);
            var executionCount = 0;
            var secondExecution = new TaskCompletionSource<int>();
            var cronJob = new FakeCronJob("0 * * * *", onExecute: _ =>
            {
                executionCount++;
                if (executionCount == 1)
                    throw new InvalidOperationException("first run fails");
                secondExecution.TrySetResult(executionCount);
            });
            var logger = NullLogger<CronJobHostedService<FakeCronJob>>.Instance;
            using var service = new CronJobHostedService<FakeCronJob>(cronJob, logger, fakeTime);

            await service.StartAsync(CancellationToken.None);
            fakeTime.Advance(TimeSpan.FromHours(1)); // fires run 1 (throws)

            // Give the loop time to re-enter and schedule the next occurrence
            await Task.Delay(50);
            fakeTime.Advance(TimeSpan.FromHours(1)); // fires run 2

            var count = await secondExecution.Task.WaitAsync(TimeSpan.FromSeconds(5), TimeProvider.System);
            Assert.Equal(2, count);

            await service.StopAsync(CancellationToken.None);
        }

        // ------------------------------------------------------------------ RunOnStartup

        [Fact]
        public async Task RunOnStartup_True_FiresOnceOnStart()
        {
            var startTime = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
            var fakeTime = new FakeTimeProvider(startTime);
            var firstExecution = new TaskCompletionSource<DateTime>();
            var cronJob = new FakeCronJob(
                "0 * * * *",
                runOnStartup: true,
                onExecute: t => firstExecution.TrySetResult(t));
            var logger = NullLogger<CronJobHostedService<FakeCronJob>>.Instance;
            using var service = new CronJobHostedService<FakeCronJob>(cronJob, logger, fakeTime);

            await service.StartAsync(CancellationToken.None);

            // Fires without any time advance
            var executedAt = await firstExecution.Task.WaitAsync(TimeSpan.FromSeconds(5), TimeProvider.System);
            Assert.Equal(1, cronJob.ExecutionCount);
            Assert.Equal(startTime.UtcDateTime, executedAt);

            await service.StopAsync(CancellationToken.None);
        }

        [Fact]
        public async Task RunOnStartup_False_DoesNotFireOnStart()
        {
            var startTime = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
            var fakeTime = new FakeTimeProvider(startTime);
            var cronJob = new FakeCronJob("0 * * * *");
            var logger = NullLogger<CronJobHostedService<FakeCronJob>>.Instance;
            using var service = new CronJobHostedService<FakeCronJob>(cronJob, logger, fakeTime);

            await service.StartAsync(CancellationToken.None);

            // Give the background loop a moment; no job should fire without time advance
            await Task.Delay(100);
            Assert.Equal(0, cronJob.ExecutionCount);

            await service.StopAsync(CancellationToken.None);
        }

        [Fact]
        public async Task RunOnStartup_DoesNotBlockHostStart()
        {
            var cronJob = new FakeCronJob(
                runOnStartup: true,
                onExecuteAsync: async (_, ct) => await Task.Delay(5_000, ct));
            var logger = NullLogger<CronJobHostedService<FakeCronJob>>.Instance;
            using var service = new CronJobHostedService<FakeCronJob>(cronJob, logger, TimeProvider.System);

            var sw = System.Diagnostics.Stopwatch.StartNew();
            await service.StartAsync(CancellationToken.None);
            sw.Stop();

            Assert.True(sw.ElapsedMilliseconds < 500,
                $"StartAsync took {sw.ElapsedMilliseconds} ms — expected < 500 ms");

            await service.StopAsync(CancellationToken.None);
        }

        [Fact]
        public async Task RunOnStartup_FailureDoesNotCrashHost()
        {
            var startTime = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
            var fakeTime = new FakeTimeProvider(startTime);
            var startupFailed = new TaskCompletionSource<bool>();
            var secondExecution = new TaskCompletionSource<bool>();
            var executionCount = 0;
            var cronJob = new FakeCronJob(
                "0 * * * *",
                runOnStartup: true,
                onExecute: _ =>
                {
                    executionCount++;
                    if (executionCount == 1)
                    {
                        startupFailed.TrySetResult(true);
                        throw new InvalidOperationException("startup run fails");
                    }
                    secondExecution.TrySetResult(true);
                });
            var logger = new RecordingLogger<CronJobHostedService<FakeCronJob>>();
            using var service = new CronJobHostedService<FakeCronJob>(cronJob, logger, fakeTime);

            await service.StartAsync(CancellationToken.None);

            // Wait for startup run to fail
            await startupFailed.Task.WaitAsync(TimeSpan.FromSeconds(5), TimeProvider.System);

            // Give the loop time to start after the startup run completes
            await Task.Delay(50);
            fakeTime.Advance(TimeSpan.FromHours(1));

            await secondExecution.Task.WaitAsync(TimeSpan.FromSeconds(5), TimeProvider.System);
            Assert.Equal(2, executionCount);

            var errors = logger.Entries.Where(e => e.Level == LogLevel.Error).ToList();
            Assert.Single(errors);

            await service.StopAsync(CancellationToken.None);
        }

        [Fact]
        public async Task RunOnStartup_FirstScheduledTickFiresAfterStartupRunCompletes()
        {
            var startTime = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
            var fakeTime = new FakeTimeProvider(startTime);
            var startupStarted = new TaskCompletionSource<bool>();
            var releaseStartup = new TaskCompletionSource<bool>();
            var secondExecution = new TaskCompletionSource<bool>();
            var executionCount = 0;
            var cronJob = new FakeCronJob(
                "0 * * * *",
                runOnStartup: true,
                onExecuteAsync: async (_, _) =>
                {
                    executionCount++;
                    if (executionCount == 1)
                    {
                        startupStarted.TrySetResult(true);
                        await releaseStartup.Task; // block until released
                    }
                    else
                    {
                        secondExecution.TrySetResult(true);
                    }
                });
            var logger = NullLogger<CronJobHostedService<FakeCronJob>>.Instance;
            using var service = new CronJobHostedService<FakeCronJob>(cronJob, logger, fakeTime);

            await service.StartAsync(CancellationToken.None);
            await startupStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), TimeProvider.System);

            // Advance past the first cron tick while startup run is still blocking
            fakeTime.Advance(TimeSpan.FromHours(1));
            await Task.Delay(50);

            // Cron loop has not started yet — only the startup run has fired
            Assert.Equal(1, executionCount);

            // Release startup; loop now starts and computes next occurrence from current fake time
            releaseStartup.TrySetResult(true);
            fakeTime.Advance(TimeSpan.FromHours(1)); // advance to the next occurrence

            await secondExecution.Task.WaitAsync(TimeSpan.FromSeconds(5), TimeProvider.System);
            Assert.Equal(2, executionCount);

            await service.StopAsync(CancellationToken.None);
        }

        [Fact]
        public async Task RunOnStartup_RespectsCancellation()
        {
            var jobStarted = new TaskCompletionSource<bool>();
            var cronJob = new FakeCronJob(
                runOnStartup: true,
                onExecuteAsync: async (_, ct) =>
                {
                    jobStarted.TrySetResult(true);
                    await Task.Delay(30_000, ct);
                });
            var logger = NullLogger<CronJobHostedService<FakeCronJob>>.Instance;
            using var service = new CronJobHostedService<FakeCronJob>(cronJob, logger, TimeProvider.System);

            await service.StartAsync(CancellationToken.None);
            await jobStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), TimeProvider.System);

            // StopAsync must complete quickly because the startup run's token is cancelled
            var stopTask = service.StopAsync(CancellationToken.None);
            var timeout = Task.Delay(TimeSpan.FromSeconds(3));
            var completed = await Task.WhenAny(stopTask, timeout);
            Assert.Same(stopTask, completed);
        }

        // ------------------------------------------------------------------ test helpers

        /// <summary>Exposes the protected <c>CronJob</c> method for unit testing.</summary>
        private sealed class TestableCronJobHostedService : CronJobHostedService<FakeCronJob>
        {
            public TestableCronJobHostedService(
                FakeCronJob cronJob,
                ILogger<CronJobHostedService<FakeCronJob>> logger,
                TimeProvider timeProvider)
                : base(cronJob, logger, timeProvider) { }

            public Task InvokeCronJobAsync(DateTime plannedTime, CancellationToken ct)
                => CronJob(plannedTime, ct);
        }
    }
}
