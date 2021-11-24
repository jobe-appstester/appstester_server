using EasyNetQ;
using Microsoft.Extensions.Configuration;

namespace AppsTester.Shared.RabbitMq
{
    public interface IRabbitBusProvider
    {
        IBus GetRabbitBus();
    }
    
    internal class RabbitBusProvider : IRabbitBusProvider
    {
        private readonly IConfiguration _configuration;

        private IBus _rabbitBus;

        public RabbitBusProvider(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public IBus GetRabbitBus()
        {
            if (_rabbitBus != null)
                return _rabbitBus;

            var connectionString =
                $"host={_configuration["Rabbit:Host"]};port=5672;prefetchcount=1;username={_configuration["Rabbit:Username"]};password={_configuration["Rabbit:Password"]}";

            _rabbitBus = RabbitHutch.CreateBus(connectionString);

            return _rabbitBus;
        }
    }
}