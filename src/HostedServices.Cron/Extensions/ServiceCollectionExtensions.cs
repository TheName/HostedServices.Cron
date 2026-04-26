using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace HostedServices.Cron.Extensions
{
    /// <summary>
    /// Extension methods for <see cref="IServiceCollection"/> to register cron-based hosted services.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Registers <typeparamref name="TCronJob"/> as a singleton, adds a
        /// <see cref="CronJobHostedService{TCronJob}"/> hosted service that executes it according
        /// to its cron schedule, and ensures a <see cref="TimeProvider"/> singleton is present
        /// (defaults to <see cref="TimeProvider.System"/> if none has been registered).
        /// </summary>
        /// <typeparam name="TCronJob">
        /// The cron job type to register. Must be a concrete class implementing <see cref="ICronJob"/>.
        /// </typeparam>
        /// <param name="serviceCollection">The <see cref="IServiceCollection"/> to add services to.</param>
        /// <returns>The same <see cref="IServiceCollection"/> so that calls can be chained.</returns>
        /// <remarks>
        /// To use a custom <see cref="TimeProvider"/> (e.g. for testing), register it before calling
        /// this method and the existing registration will not be overwritten:
        /// <code>
        /// services.AddSingleton&lt;TimeProvider&gt;(myFakeTimeProvider);
        /// services.AddCronJobHostedService&lt;MyJob&gt;();
        /// </code>
        /// </remarks>
        public static IServiceCollection AddCronJobHostedService<TCronJob>(this IServiceCollection serviceCollection)
            where TCronJob : class, ICronJob
        {
            serviceCollection.TryAddSingleton(TimeProvider.System);
            return serviceCollection
                .AddSingleton<TCronJob>()
                .AddHostedService<CronJobHostedService<TCronJob>>();
        }
    }
}
