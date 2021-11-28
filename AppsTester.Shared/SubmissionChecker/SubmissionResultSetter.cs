using System.Threading;
using System.Threading.Tasks;
using AppsTester.Shared.RabbitMq;
using EasyNetQ;

namespace AppsTester.Shared.SubmissionChecker
{
    public interface ISubmissionResultSetter
    {
        Task SetResultAsync<TResult>(TResult result, CancellationToken cancellationToken);
    }
    
    public class SubmissionResultSetter : SubmissionProcessor, ISubmissionResultSetter
    {
        private readonly IRabbitBusProvider _rabbitBusProvider;

        public SubmissionResultSetter(IRabbitBusProvider rabbitBusProvider)
        {
            _rabbitBusProvider = rabbitBusProvider;
        }

        public async Task SetResultAsync<TResult>(TResult result, CancellationToken cancellationToken)
        {
            using var rabbitConnection = _rabbitBusProvider.GetRabbitBus();

            await rabbitConnection.PubSub.PublishAsync(message: result, topic: "", cancellationToken);
        }
    }
}