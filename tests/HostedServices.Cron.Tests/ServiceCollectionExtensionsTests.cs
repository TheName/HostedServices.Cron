using HostedServices.Cron.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace HostedServices.Cron.Tests
{
    public class ServiceCollectionExtensionsTests
    {
        [Fact]
        public void AddCronJobHostedService_RegistersCronJobAsSingleton()
        {
            var services = new ServiceCollection();

            services.AddCronJobHostedService<FakeCronJob>();

            var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(FakeCronJob));
            Assert.NotNull(descriptor);
            Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
        }

        [Fact]
        public void AddCronJobHostedService_RegistersHostedServiceWithCorrectImplementationType()
        {
            var services = new ServiceCollection();

            services.AddCronJobHostedService<FakeCronJob>();

            var descriptor = services.FirstOrDefault(d =>
                d.ServiceType == typeof(IHostedService) &&
                d.ImplementationType == typeof(CronJobHostedService<FakeCronJob>));
            Assert.NotNull(descriptor);
            Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
        }

        [Fact]
        public void AddCronJobHostedService_RegistersTimeProviderSystemAsSingleton()
        {
            var services = new ServiceCollection();

            services.AddCronJobHostedService<FakeCronJob>();

            var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(TimeProvider));
            Assert.NotNull(descriptor);
            Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
            Assert.Same(TimeProvider.System, descriptor.ImplementationInstance);
        }

        [Fact]
        public void AddCronJobHostedService_DoesNotOverrideExistingTimeProvider()
        {
            var services = new ServiceCollection();
            var customTimeProvider = new FakeTimeProvider();
            services.AddSingleton<TimeProvider>(customTimeProvider);

            services.AddCronJobHostedService<FakeCronJob>();

            var descriptors = services.Where(d => d.ServiceType == typeof(TimeProvider)).ToList();
            Assert.Single(descriptors); // TryAdd must not add a second one
            Assert.Same(customTimeProvider, descriptors[0].ImplementationInstance);
        }

        [Fact]
        public void AddCronJobHostedService_ReturnsSameServiceCollection()
        {
            var services = new ServiceCollection();

            var result = services.AddCronJobHostedService<FakeCronJob>();

            Assert.Same(services, result);
        }

        [Fact]
        public void AddCronJobHostedService_CalledTwice_RegistersTwoCronJobHostedServices()
        {
            var services = new ServiceCollection();

            services.AddCronJobHostedService<FakeCronJob>();
            services.AddCronJobHostedService<AnotherFakeCronJob>();

            var hostedServiceDescriptors = services
                .Where(d => d.ServiceType == typeof(IHostedService))
                .ToList();

            Assert.Equal(2, hostedServiceDescriptors.Count);
        }

        private sealed class AnotherFakeCronJob : FakeCronJob
        {
            public AnotherFakeCronJob() : base("30 * * * *") { }
        }
    }
}
