using AppsTester.Shared.RabbitMq;
using Microsoft.Extensions.DependencyInjection;

namespace AppsTester.Shared.SubmissionChecker
{
    public static class SubmissionCheckerExtensions
    {
        public static IServiceCollection AddSubmissionChecker<TSubmissionChecker>(
            this IServiceCollection serviceCollection, string checkerSystemName, ushort parallelExecutions = 1)
            where TSubmissionChecker : class, ISubmissionChecker
        {
            serviceCollection.AddHostedService(
                serviceProvider => new SubmissionCheckerBackgroundService<TSubmissionChecker>(
                    checkerSystemName,
                    serviceProvider.GetRequiredService<IRabbitBusProvider>(),
                    serviceProvider.GetRequiredService<IServiceScopeFactory>(),
                    prefetch: parallelExecutions));

            serviceCollection.AddScoped<TSubmissionChecker>();

            serviceCollection.AddScoped<ISubmissionProcessingContextAccessor, SubmissionProcessingContextAccessor>();
            serviceCollection.AddScoped<ISubmissionPlainParametersProvider, SubmissionPlainParametersProvider>();
            serviceCollection.AddScoped<ISubmissionFilesProvider, SubmissionFilesProvider>();
            serviceCollection.AddScoped<ISubmissionStatusSetter, SubmissionStatusSetter>();
            serviceCollection.AddScoped<ISubmissionResultSetter, SubmissionResultSetter>();
            serviceCollection.AddScoped<ISubmissionProcessingLogger, SubmissionProcessingLogger<TSubmissionChecker>>();

            return serviceCollection;
        }
    }
}