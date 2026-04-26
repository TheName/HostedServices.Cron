# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0] - 2026-04-26

### Added

- `ICronJob` interface — implement `CronExpression` and `ExecuteAsync` to define a recurring job.
- `CronJobHostedService<TCronJob>` — `BackgroundService` that drives a single `ICronJob` on its cron schedule. Unhandled exceptions from job execution are caught, logged as `Error`, and the loop continues with the next occurrence. `OperationCanceledException` during application shutdown is logged at `Information` level and the service exits cleanly.
- `CronJobHostedService` — abstract base class for custom hosted-service subclasses. `CronJobType` is `virtual` (defaults to `GetType()`) so direct subclasses do not need to override it.
- `ServiceCollectionExtensions.AddCronJobHostedService<TCronJob>` — one-call DI registration that registers the job as a singleton, wires up the hosted service, and ensures a `TimeProvider` singleton is present (defaults to `TimeProvider.System`).
- **Seconds-precision scheduling** — 6-field cron expressions (e.g. `"*/30 * * * * *"`) are supported alongside the standard 5-field format. The format is detected automatically from the number of fields.
- **`TimeProvider` injection** — scheduling uses the injected `TimeProvider`, making timing fully controllable in tests via `FakeTimeProvider` (from `Microsoft.Extensions.TimeProvider.Testing`) or any custom implementation.
- XML documentation on all public APIs.
- Multi-target support: `net8.0`, `netstandard2.1`, `netstandard2.0`, and `net472`.
- Unit and integration tests (18 tests via xUnit) covering constructor validation, job delegation, DI registration, scheduling with `FakeTimeProvider`, seconds-precision execution, and loop resilience after job failure.
