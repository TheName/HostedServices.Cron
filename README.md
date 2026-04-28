# HostedServices.Cron

[![NuGet](https://img.shields.io/nuget/v/TheName.HostedServices.Cron)](https://www.nuget.org/packages/TheName.HostedServices.Cron)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

Simple cron-based hosted services for .NET applications, built on top of `IHostedService` with minimal dependencies.

## Installation

```bash
dotnet add package TheName.HostedServices.Cron
```

## Quick start

**1. Implement `ICronJob` (or extend `CronJobBase`):**

```csharp
using HostedServices.Cron;

// Extend CronJobBase to get RunOnStartup = false by default,
// or implement ICronJob directly and supply all members yourself.
public class ReportJob : CronJobBase
{
    // Run at 08:00 every day
    public override string CronExpression => "0 8 * * *";

    public override async Task ExecuteAsync(DateTime plannedExecutionTime, CancellationToken cancellationToken)
    {
        // your work here
        await GenerateReportAsync(cancellationToken);
    }
}
```

**2. Register with the DI container:**

```csharp
using HostedServices.Cron.Extensions;

// Program.cs / Startup.cs
builder.Services.AddCronJobHostedService<ReportJob>();
```

That's it. The job starts automatically with your application and runs according to its cron schedule.

## How it works

- The cron expression on `ICronJob.CronExpression` is parsed once using the [Cronos](https://github.com/HangfireIO/Cronos) library. The format (standard 5-field or seconds-precision 6-field) is detected automatically from the number of fields.
- A `BackgroundService` loop calculates the next occurrence using `TimeProvider`, sleeps until that time, then calls `ICronJob.ExecuteAsync`.
- **Exceptions are caught and logged** — the loop continues with the next occurrence so a single failure does not kill the service.
- **Clean shutdown** — if the application stops while a job is executing, the resulting `OperationCanceledException` is logged at `Information` level (not `Error`) and the service exits cleanly.
- All scheduled times are in **UTC**.

## Running the job immediately on start-up

Override `RunOnStartup` on the job class to fire once as soon as the host starts, then continue on the normal cron schedule:

```csharp
public class CatalogSyncJob : CronJobBase
{
    public override string CronExpression => "0 0 */12 * * *"; // every 12 h

    // Fire immediately on start-up to backfill, then run on the cron schedule
    public override bool RunOnStartup => true;

    public override async Task ExecuteAsync(DateTime plannedExecutionTime, CancellationToken cancellationToken)
    {
        await SyncCatalogAsync(cancellationToken);
    }
}
```

Registration is unchanged:

```csharp
builder.Services.AddCronJobHostedService<CatalogSyncJob>();
```

The start-up run executes in the background — `IHost.StartAsync` returns promptly so HTTP listeners are not delayed. The first scheduled tick fires only after the start-up run completes, so there is no concurrent overlap between the two. Exceptions from the start-up run are logged at `Error` level with a `(startup run)` marker and the normal schedule continues. The run honours application shutdown: if the host stops mid-run the job's `CancellationToken` is cancelled.

`RunOnStartup` defaults to `false` (via `CronJobBase`), so existing jobs require no changes.

## API reference

### `ICronJob`

| Member | Description |
|--------|-------------|
| `string CronExpression { get; }` | 5-field standard or 6-field seconds-precision cron expression. Format is detected automatically. |
| `bool RunOnStartup { get; }` | When `true`, fires the job once immediately on host start-up before the first scheduled tick. |
| `Task ExecuteAsync(DateTime plannedExecutionTime, CancellationToken cancellationToken)` | Invoked at each execution. `plannedExecutionTime` is the scheduled UTC time, or the UTC time of host start-up for a `RunOnStartup` execution. |

### `CronJobBase`

Abstract base class that implements `ICronJob` with `RunOnStartup` defaulting to `false`. Extend this instead of implementing `ICronJob` directly when you don't need a start-up run.

### `CronJobHostedService<TCronJob>`

The hosted service that drives execution. Registered automatically by `AddCronJobHostedService<TCronJob>`. You only need to reference this type directly if you are wiring up DI manually or writing custom hosting infrastructure.

### `ServiceCollectionExtensions.AddCronJobHostedService<TCronJob>`

```csharp
IServiceCollection AddCronJobHostedService<TCronJob>(this IServiceCollection serviceCollection)
    where TCronJob : class, ICronJob
```

Registers `TCronJob` as a **singleton**, adds `CronJobHostedService<TCronJob>` as a hosted service, and ensures a `TimeProvider` singleton is present (defaults to `TimeProvider.System` if none has been registered).

## Cron expression format

The library delegates expression parsing to [Cronos](https://github.com/HangfireIO/Cronos). The format is detected automatically from the number of space-separated fields.

### Standard 5-field

```
┌─ minute      (0–59)
│ ┌─ hour      (0–23)
│ │ ┌─ day of month (1–31)
│ │ │ ┌─ month (1–12 or JAN–DEC)
│ │ │ │ ┌─ day of week  (0–7, 0 and 7 are Sunday, or SUN–SAT)
│ │ │ │ │
* * * * *
```

| Expression | Meaning |
|------------|---------|
| `* * * * *` | Every minute |
| `0 * * * *` | Every hour |
| `0 8 * * *` | Every day at 08:00 UTC |
| `0 8 * * 1` | Every Monday at 08:00 UTC |
| `*/15 * * * *` | Every 15 minutes |
| `0 0 1 * *` | First day of every month at midnight |

### Seconds-precision 6-field

Add a seconds field as the first field:

```
┌─ second      (0–59)
│ ┌─ minute    (0–59)
│ │ ┌─ hour    (0–23)
│ │ │ ┌─ day of month (1–31)
│ │ │ │ ┌─ month (1–12 or JAN–DEC)
│ │ │ │ │ ┌─ day of week  (0–7)
│ │ │ │ │ │
* * * * * *
```

| Expression | Meaning |
|------------|---------|
| `*/30 * * * * *` | Every 30 seconds |
| `0 * * * * *` | Every minute at second 0 |
| `0 */5 * * * *` | Every 5 minutes at second 0 |

## Testing with a custom `TimeProvider`

`AddCronJobHostedService` registers `TimeProvider.System` only if no `TimeProvider` is already present in the container. To control time in tests, register a custom implementation first:

```csharp
// Using Microsoft.Extensions.TimeProvider.Testing
var fakeTime = new FakeTimeProvider(startTime);
services.AddSingleton<TimeProvider>(fakeTime);
services.AddCronJobHostedService<MyJob>();

// ...start the host, then advance time to trigger execution:
fakeTime.Advance(TimeSpan.FromHours(1));
```

## Target frameworks

| Framework | Supported |
|-----------|-----------|
| .NET 8 | ✓ |
| .NET Standard 2.1 | ✓ |
| .NET Standard 2.0 | ✓ |
| .NET Framework 4.7.2 | ✓ |

## License

[MIT](LICENSE)
