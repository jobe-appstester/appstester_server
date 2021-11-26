using Microsoft.Extensions.DependencyInjection;

namespace AppsTester.Shared.Files
{
    public static class TemporaryFolderExtensions
    {
        public static IServiceCollection AddTemporaryFolders(this IServiceCollection serviceCollection)
        {
            serviceCollection.AddSingleton<ITemporaryFolderProvider, TemporaryFolderProvider>();

            return serviceCollection;
        }
    }
}