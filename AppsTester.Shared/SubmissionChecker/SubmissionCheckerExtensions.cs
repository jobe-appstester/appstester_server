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

            serviceCollection.AddScoped<ISubmissionProcessor, SubmissionPlainParametersProvider>();
            serviceCollection.AddScoped<ISubmissionPlainParametersProvider, SubmissionPlainParametersProvider>();

            serviceCollection.AddScoped<ISubmissionProcessor, SubmissionFilesProvider>();
            serviceCollection.AddScoped<ISubmissionFilesProvider, SubmissionFilesProvider>();

            serviceCollection.AddScoped<ISubmissionProcessor, SubmissionStatusSetter>();
            serviceCollection.AddScoped<ISubmissionStatusSetter, SubmissionStatusSetter>();

            serviceCollection.AddScoped<ISubmissionProcessor, SubmissionResultSetter>();
            serviceCollection.AddScoped<ISubmissionResultSetter, SubmissionResultSetter>();

            return serviceCollection;
        }
    }
}