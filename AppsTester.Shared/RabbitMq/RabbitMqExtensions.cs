using Microsoft.Extensions.DependencyInjection;

namespace AppsTester.Shared.RabbitMq
{
    public static class RabbitMqExtensions
    {
        public static IServiceCollection AddRabbitMq(this IServiceCollection serviceCollection)
        {
            serviceCollection.AddSingleton<IRabbitBusProvider, RabbitBusProvider>();

            return serviceCollection;
        }
    }
}