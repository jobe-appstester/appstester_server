using AppsTester.Shared.RabbitMq;
using Microsoft.Extensions.DependencyInjection;

namespace AppsTester.Shared.SubmissionChecker
{
    public static class SubmissionCheckerExtensions
    {
        public static IServiceCollection AddSubmissionChecker<TSubmissionChecker>(
            this IServiceCollection serviceCollection, string checkerSystemName)
            where TSubmissionChecker : class, ISubmissionChecker
        {
            serviceCollection.AddHostedService(
                serviceProvider => new SubmissionCheckerBackgroundService<TSubmissionChecker>(
                    checkerSystemName,
                    serviceProvider.GetRequiredService<IRabbitBusProvider>(),
                    serviceProvider.GetRequiredService<IServiceScopeFactory>()));

            serviceCollection.AddScoped<TSubmissionChecker>();

            serviceCollection.AddScoped<ISubmissionPlainParametersProvider, SubmissionPlainParametersProvider>();
            serviceCollection.AddScoped<ISubmissionFilesProvider, SubmissionFilesProvider>();
            serviceCollection.AddScoped<ISubmissionStatusSetter, SubmissionStatusSetter>();
            serviceCollection.AddScoped<ISubmissionResultSetter, SubmissionResultSetter>();

            return serviceCollection;
        }
    }
}