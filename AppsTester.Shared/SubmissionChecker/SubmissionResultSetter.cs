using System.Threading.Tasks;
using AppsTester.Shared.RabbitMq;
using AppsTester.Shared.SubmissionChecker.Events;
using EasyNetQ;

namespace AppsTester.Shared.SubmissionChecker
{
    public interface ISubmissionResultSetter
    {
        Task SetResultAsync(object result);
    }

    public class SubmissionResultSetter : ISubmissionResultSetter
    {
        private readonly IRabbitBusProvider _rabbitBusProvider;
        private readonly ISubmissionProcessingContextAccessor _processingContextAccessor;

        public SubmissionResultSetter(
            IRabbitBusProvider rabbitBusProvider,
            ISubmissionProcessingContextAccessor processingContextAccessor)
        {
            _rabbitBusProvider = rabbitBusProvider;
            _processingContextAccessor = processingContextAccessor;
        }

        public async Task SetResultAsync(object result)
        {
            var rabbitConnection = _rabbitBusProvider.GetRabbitBus();

            var submissionCheckResultEvent =
                new SubmissionCheckResultEvent
                    {
                        SubmissionId = _processingContextAccessor.ProcessingContext.Event.SubmissionId
                    }
                    .WithResult(result);

            await rabbitConnection.PubSub.PublishAsync(
                message: submissionCheckResultEvent,
                topic: "",
                _processingContextAccessor.ProcessingContext.CancellationToken);
        }
    }
}