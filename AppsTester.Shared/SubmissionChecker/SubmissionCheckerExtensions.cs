using Microsoft.Extensions.DependencyInjection;

namespace AppsTester.Shared.SubmissionChecker
{
    public static class SubmissionCheckerExtensions
    {
        public static IServiceCollection AddSubmissionChecker<TSubmissionChecker>(
            this IServiceCollection serviceCollection, string checkerSystemName)
            where TSubmissionChecker : class, ISubmissionChecker
        {
            serviceCollection.AddHostedService<SubmissionCheckerBackgroundService<TSubmissionChecker>>();

            serviceCollection.AddScoped<ISubmissionChecker, TSubmissionChecker>();

            serviceCollection.AddScoped<ISubmissionPlainParametersProvider, SubmissionPlainParametersProvider>();
            serviceCollection.AddScoped<ISubmissionFilesProvider, SubmissionFilesProvider>();
            serviceCollection.AddScoped<ISubmissionStatusSetter, SubmissionStatusSetter>();
            serviceCollection.AddScoped<ISubmissionResultSetter, SubmissionResultSetter>();

            return serviceCollection;
        }
    }
}