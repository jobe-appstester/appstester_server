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
            serviceCollection.AddScoped(provider =>
                provider.GetRequiredService<ISubmissionPlainParametersProvider>() as ISubmissionProcessor);

            serviceCollection.AddScoped<ISubmissionFilesProvider, SubmissionFilesProvider>();
            serviceCollection.AddScoped(provider =>
                provider.GetRequiredService<ISubmissionFilesProvider>() as ISubmissionProcessor);

            serviceCollection.AddScoped<ISubmissionStatusSetter, SubmissionStatusSetter>();
            serviceCollection.AddScoped(provider =>
                provider.GetRequiredService<ISubmissionStatusSetter>() as ISubmissionProcessor);

            serviceCollection.AddScoped<ISubmissionResultSetter, SubmissionResultSetter>();
            serviceCollection.AddScoped(provider =>
                provider.GetRequiredService<ISubmissionResultSetter>() as ISubmissionProcessor);

            return serviceCollection;
        }
    }
}