using System;
using System.Threading;
using System.Threading.Tasks;

namespace HostedServices.Cron
{
    /// <summary>
    /// Convenience abstract base class that implements <see cref="ICronJob"/> with a default
    /// <see cref="RunOnStartup"/> value of <see langword="false"/>.
    /// </summary>
    /// <remarks>
    /// Derive from this class instead of implementing <see cref="ICronJob"/> directly when you want
    /// the standard schedule-only behaviour without having to write <c>public bool RunOnStartup =&gt; false;</c>.
    /// To opt in to a start-up run, override <see cref="RunOnStartup"/> and return <see langword="true"/>.
    /// </remarks>
    public abstract class CronJobBase : ICronJob
    {
        /// <inheritdoc/>
        public abstract string CronExpression { get; }

        /// <inheritdoc/>
        public virtual bool RunOnStartup => false;

        /// <inheritdoc/>
        public abstract Task ExecuteAsync(DateTime plannedExecutionTime, CancellationToken cancellationToken);
    }
}
