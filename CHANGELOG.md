# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0] - 2026-04-26

### Added

- `ICronJob` interface — implement `CronExpression` and `ExecuteAsync` to define a recurring job.
- `CronJobHostedService<TCronJob>` — `BackgroundService` that drives a single `ICronJob` on its cron schedule. Unhandled exceptions from job execution are caught, logged as `Error`, and the loop continues with the next occurrence. `OperationCanceledException` during application shutdown is logged at `Information` level and the service exits cleanly.
- `CronJobHostedService` — abstract base class for custom hosted-service subclasses. `CronJobType` is `virtual` (defaults to `GetType()`) so direct subclasses do not need to override it.
- `ServiceCollectionExtensions.AddCronJobHostedService<TCronJob>` — one-call DI registration that registers the job as a singleton and wires up the hosted service.
- XML documentation on all public APIs.
- Multi-target support: `net8.0`, `netstandard2.1`, `netstandard2.0`, and `net472`.
- Unit tests (12 tests via xUnit) covering constructor validation, job delegation, DI registration, and shutdown behaviour.
