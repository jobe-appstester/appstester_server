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

    public class SubmissionStatusSetter : ISubmissionStatusSetter
    {
        private readonly IRabbitBusProvider _rabbitBusProvider;
        private readonly ISubmissionProcessingContextAccessor _processingContextAccessor;

        private readonly SemaphoreSlim _semaphore = new(initialCount: 1);
        private int _version = 1;

        public SubmissionStatusSetter(
            IRabbitBusProvider rabbitBusProvider,
            ISubmissionProcessingContextAccessor processingContextAccessor)
        {
            _rabbitBusProvider = rabbitBusProvider;
            _processingContextAccessor = processingContextAccessor;
        }

        public async Task SetStatusAsync<TStatus>(TStatus status)
        {
            await _semaphore.WaitAsync(_processingContextAccessor.ProcessingContext.CancellationToken);

            var submissionCheckStatusEvent =
                new SubmissionCheckStatusEvent
                    {
                        SubmissionId = _processingContextAccessor.ProcessingContext.Event.SubmissionId,
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
                _processingContextAccessor.ProcessingContext.CancellationToken);
        }

        private void IncreaseVersion()
        {
            _version++;
        }
    }
}