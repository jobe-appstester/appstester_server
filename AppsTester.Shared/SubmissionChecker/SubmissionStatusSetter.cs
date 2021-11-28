using System.Threading;
using System.Threading.Tasks;
using AppsTester.Shared.Events;
using AppsTester.Shared.RabbitMq;
using EasyNetQ;

namespace AppsTester.Shared.SubmissionChecker
{
    public interface ISubmissionStatusSetter
    {
        Task SetStatusAsync<TStatus>(TStatus status, CancellationToken cancellationToken);
    }
    
    public class SubmissionStatusSetter : SubmissionProcessor, ISubmissionStatusSetter
    {
        private readonly IRabbitBusProvider _rabbitBusProvider;

        public SubmissionStatusSetter(IRabbitBusProvider rabbitBusProvider)
        {
            _rabbitBusProvider = rabbitBusProvider;
        }

        public async Task SetStatusAsync<TStatus>(TStatus status, CancellationToken cancellationToken)
        {
            var rabbitConnection = _rabbitBusProvider.GetRabbitBus();

            var submissionCheckStatusEvent =
                new SubmissionCheckStatusEvent { SubmissionId = SubmissionCheckRequestEvent.SubmissionId }
                    .WithStatus(status);

            await rabbitConnection.PubSub.PublishAsync(
                message: submissionCheckStatusEvent,
                topic: "",
                cancellationToken);
        }
    }
}