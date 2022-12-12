using System.Threading.Tasks;
using AppsTester.Shared.RabbitMq;
using AppsTester.Shared.SubmissionChecker.Events;
using EasyNetQ;
using Microsoft.Extensions.Logging;

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
        private readonly ILogger<SubmissionResultSetter> _logger;

        public SubmissionResultSetter(
            IRabbitBusProvider rabbitBusProvider,
            ISubmissionProcessingContextAccessor processingContextAccessor,
            ILogger<SubmissionResultSetter> logger)
        {
            _rabbitBusProvider = rabbitBusProvider;
            _processingContextAccessor = processingContextAccessor;
            _logger = logger;
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
            _logger.LogInformation("{SubmissionId} done with result {SerializedResult}", submissionCheckResultEvent.SubmissionId, submissionCheckResultEvent.SerializedResult);
            await rabbitConnection.PubSub.PublishAsync(
                message: submissionCheckResultEvent,
                topic: "",
                _processingContextAccessor.ProcessingContext.CancellationToken);
        }
    }
}