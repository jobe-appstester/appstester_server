using System.Threading;
using System.Threading.Tasks;
using AppsTester.Shared.RabbitMq;
using AppsTester.Shared.SubmissionChecker.Events;
using EasyNetQ;

namespace AppsTester.Shared.SubmissionChecker
{
    public interface ISubmissionStatusSetter
    {
        Task SetStatusAsync<TStatus>(TStatus status);
    }

    public class SubmissionStatusSetter : SubmissionProcessor, ISubmissionStatusSetter
    {
        private readonly IRabbitBusProvider _rabbitBusProvider;
        private readonly SemaphoreSlim _semaphore = new(initialCount: 1);
        private int _version = 1;

        public SubmissionStatusSetter(IRabbitBusProvider rabbitBusProvider)
        {
            _rabbitBusProvider = rabbitBusProvider;
        }

        public async Task SetStatusAsync<TStatus>(TStatus status)
        {
            await _semaphore.WaitAsync(SubmissionProcessingContext.CancellationToken);

            var submissionCheckStatusEvent =
                new SubmissionCheckStatusEvent
                    {
                        SubmissionId = SubmissionCheckRequestEvent.SubmissionId,
                        Version = _version
                    }
                    .WithStatus(status);

            await PublishStatusMessageAsync(submissionCheckStatusEvent);

            IncreaseVersion();

            _semaphore.Release();
        }

        private async Task PublishStatusMessageAsync(SubmissionCheckStatusEvent submissionCheckStatusEvent)
        {
            var rabbitConnection = _rabbitBusProvider.GetRabbitBus();

            await rabbitConnection.PubSub.PublishAsync(
                message: submissionCheckStatusEvent,
                topic: "",
                SubmissionProcessingContext.CancellationToken);
        }

        private void IncreaseVersion()
        {
            _version++;
        }
    }
}