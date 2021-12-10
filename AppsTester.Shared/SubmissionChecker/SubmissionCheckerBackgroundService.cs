using System;
using System.Threading;
using System.Threading.Tasks;
using AppsTester.Shared.RabbitMq;
using AppsTester.Shared.SubmissionChecker.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AppsTester.Shared.SubmissionChecker
{
    public class SubmissionCheckerBackgroundService<TSubmissionChecker> : BackgroundService
        where TSubmissionChecker : class, ISubmissionChecker
    {
        private readonly string _checkerSystemName;
        private readonly IRabbitBusProvider _rabbitBusProvider;
        private readonly IServiceScopeFactory _serviceScopeFactory;

        public SubmissionCheckerBackgroundService(
            string checkerSystemName,
            IRabbitBusProvider rabbitBusProvider,
            IServiceScopeFactory serviceScopeFactory)
        {
            _checkerSystemName = checkerSystemName;
            _rabbitBusProvider = rabbitBusProvider;
            _serviceScopeFactory = serviceScopeFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var rabbitConnection = _rabbitBusProvider.GetRabbitBus();

            await rabbitConnection
                .PubSub
                .SubscribeAsync<SubmissionCheckRequestEvent>(
                    subscriptionId: _checkerSystemName,
                    onMessage: async (request, cancellationToken) =>
                    {
                        try
                        {
                            using var scope = _serviceScopeFactory.CreateScope();

                            var processingContext =
                                new SubmissionProcessingContext(Event: request, cancellationToken);

                            var submissionProcessingContextAccessor = scope
                                .ServiceProvider
                                .GetService<ISubmissionProcessingContextAccessor>();

                            ((SubmissionProcessingContextAccessor)submissionProcessingContextAccessor)!
                                .SetProcessingContext(processingContext);

                            var submissionChecker = scope
                                .ServiceProvider
                                .GetRequiredService<TSubmissionChecker>();

                            await submissionChecker.CheckSubmissionAsync(processingContext);
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e);

                            await rabbitConnection
                                .Scheduler
                                .FuturePublishAsync(
                                    request,
                                    delay: TimeSpan.FromMinutes(1),
                                    configure: configuration => configuration.WithTopic(_checkerSystemName),
                                    cancellationToken);
                        }
                    },
                    configure: configuration => configuration.WithPrefetchCount(1).WithTopic(_checkerSystemName),
                    cancellationToken: stoppingToken
                );

            await Task.Delay(millisecondsDelay: -1, stoppingToken);
        }
    }
}