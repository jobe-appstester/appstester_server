using System.Threading;
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
    
    public class SubmissionResultSetter : SubmissionProcessor, ISubmissionResultSetter
    {
        private readonly IRabbitBusProvider _rabbitBusProvider;

        public SubmissionResultSetter(IRabbitBusProvider rabbitBusProvider)
        {
            _rabbitBusProvider = rabbitBusProvider;
        }

        public async Task SetResultAsync(object result)
        {
            var rabbitConnection = _rabbitBusProvider.GetRabbitBus();

            var submissionCheckResultEvent =
                new SubmissionCheckResultEvent { SubmissionId = SubmissionCheckRequestEvent.SubmissionId }
                    .WithResult(result);

            await rabbitConnection.PubSub.PublishAsync(
                message: submissionCheckResultEvent,
                topic: "",
                SubmissionProcessingContext.CancellationToken);
        }
    }
}