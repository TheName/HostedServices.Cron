using Microsoft.Extensions.DependencyInjection;

namespace HostedServices.Cron.Extensions
{
    /// <summary>
    /// Extension methods for <see cref="IServiceCollection"/> to register cron-based hosted services.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Registers <typeparamref name="TCronJob"/> as a singleton and adds a
        /// <see cref="CronJobHostedService{TCronJob}"/> hosted service that executes it
        /// according to its cron schedule.
        /// </summary>
        /// <typeparam name="TCronJob">
        /// The cron job type to register. Must be a concrete class implementing <see cref="ICronJob"/>.
        /// </typeparam>
        /// <param name="serviceCollection">The <see cref="IServiceCollection"/> to add services to.</param>
        /// <returns>The same <see cref="IServiceCollection"/> so that calls can be chained.</returns>
        public static IServiceCollection AddCronJobHostedService<TCronJob>(this IServiceCollection serviceCollection)
            where TCronJob : class, ICronJob
        {
            return serviceCollection
                .AddSingleton<TCronJob>()
                .AddHostedService<CronJobHostedService<TCronJob>>();
        }
    }
}
