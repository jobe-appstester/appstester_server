using System;
using System.Threading;
using System.Threading.Tasks;
using AppsTester.Shared.RabbitMq;
using AppsTester.Shared.SubmissionChecker.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Sentry;

namespace AppsTester.Shared.SubmissionChecker
{
    public class SubmissionCheckerBackgroundService<TSubmissionChecker> : BackgroundService
        where TSubmissionChecker : class, ISubmissionChecker
    {
        private readonly string _checkerSystemName;
        private readonly IRabbitBusProvider _rabbitBusProvider;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly ushort _prefetch;

        public SubmissionCheckerBackgroundService(
            string checkerSystemName,
            IRabbitBusProvider rabbitBusProvider,
            IServiceScopeFactory serviceScopeFactory,
            ushort prefetch)
        {
            _checkerSystemName = checkerSystemName;
            _rabbitBusProvider = rabbitBusProvider;
            _serviceScopeFactory = serviceScopeFactory;
            _prefetch = prefetch;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var submissionsMutex = new SemaphoreSlim(initialCount: 1);

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
                                new SubmissionProcessingContext(
                                    Event: request,
                                    SubmissionsMutex: submissionsMutex,
                                    CancellationToken: cancellationToken);

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
                            SentrySdk.CaptureException(e);

                            await rabbitConnection
                                .Scheduler
                                .FuturePublishAsync(
                                    request,
                                    delay: TimeSpan.FromMinutes(1),
                                    configure: configuration => configuration.WithTopic(_checkerSystemName),
                                    cancellationToken);
                        }
                    },
                    configure: configuration =>
                        configuration
                            .WithPrefetchCount(_prefetch)
                            .WithTopic(_checkerSystemName),
                    cancellationToken: stoppingToken
                );

            await Task.Delay(millisecondsDelay: -1, stoppingToken);
        }
    }
}