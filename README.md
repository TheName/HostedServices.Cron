# HostedServices.Cron

[![NuGet](https://img.shields.io/nuget/v/TheName.HostedServices.Cron)](https://www.nuget.org/packages/TheName.HostedServices.Cron)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

Simple cron-based hosted services for .NET applications, built on top of `IHostedService` with minimal dependencies.

## Installation

```bash
dotnet add package TheName.HostedServices.Cron
```

## Quick start

**1. Implement `ICronJob`:**

```csharp
using HostedServices.Cron;

public class ReportJob : ICronJob
{
    // Run at 08:00 every day
    public string CronExpression => "0 8 * * *";

    public async Task ExecuteAsync(DateTime plannedExecutionTime, CancellationToken cancellationToken)
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

- The cron expression on `ICronJob.CronExpression` is parsed once using the [Cronos](https://github.com/HangfireIO/Cronos) library.
- A `BackgroundService` loop calculates the next occurrence, sleeps until that time, then calls `ICronJob.ExecuteAsync`.
- **Exceptions are caught and logged** — the loop continues with the next occurrence so a single failure does not kill the service.
- **Clean shutdown** — if the application stops while a job is executing, the resulting `OperationCanceledException` is logged at `Information` level (not `Error`) and the service exits cleanly.
- All scheduled times are in **UTC**.

## API reference

### `ICronJob`

| Member | Description |
|--------|-------------|
| `string CronExpression { get; }` | Standard 5-field cron expression (e.g. `"*/5 * * * *"` for every 5 minutes). |
| `Task ExecuteAsync(DateTime plannedExecutionTime, CancellationToken cancellationToken)` | Invoked at each scheduled occurrence. `plannedExecutionTime` is the UTC time the run was scheduled for. |

### `CronJobHostedService<TCronJob>`

The hosted service that drives execution. Registered automatically by `AddCronJobHostedService<TCronJob>`. You only need to reference this type directly if you are wiring up DI manually or writing custom hosting infrastructure.

### `ServiceCollectionExtensions.AddCronJobHostedService<TCronJob>`

```csharp
IServiceCollection AddCronJobHostedService<TCronJob>(this IServiceCollection serviceCollection)
    where TCronJob : class, ICronJob
```

Registers `TCronJob` as a **singleton** and adds `CronJobHostedService<TCronJob>` as a hosted service.

## Cron expression format

The library delegates expression parsing to [Cronos](https://github.com/HangfireIO/Cronos), which supports the standard 5-field format:

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

## Target frameworks

| Framework | Supported |
|-----------|-----------|
| .NET 8 | ✓ |
| .NET Standard 2.1 | ✓ |
| .NET Standard 2.0 | ✓ |
| .NET Framework 4.7.2 | ✓ |

## License

[MIT](LICENSE)
