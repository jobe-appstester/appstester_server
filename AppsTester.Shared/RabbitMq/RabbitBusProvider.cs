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

            _rabbitBus = RabbitHutch.CreateBus(_configuration.GetConnectionString("RabbitMq"));

            return _rabbitBus;
        }
    }
}