using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AppsTester.Controller.Moodle;
using AppsTester.Shared.RabbitMq;
using AppsTester.Shared.SubmissionChecker.Events;
using EasyNetQ;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Sentry;

namespace AppsTester.Controller.Submissions
{
    internal class SubscriptionCheckStatusesProcessor : BackgroundService
    {
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly IRabbitBusProvider _rabbitBusProvider;
        private readonly SemaphoreSlim _semaphore = new(initialCount: 1);
        private readonly IMoodleCommunicator _moodleCommunicator;

        public SubscriptionCheckStatusesProcessor(
            IServiceScopeFactory serviceScopeFactory,
            IRabbitBusProvider rabbitBusProvider,
            IMoodleCommunicator moodleCommunicator)
        {
            _serviceScopeFactory = serviceScopeFactory;
            _rabbitBusProvider = rabbitBusProvider;
            _moodleCommunicator = moodleCommunicator;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var rabbitConnection = _rabbitBusProvider.GetRabbitBus();

            await rabbitConnection
                .PubSub
                .SubscribeAsync<SubmissionCheckStatusEvent>(
                    subscriptionId: "",
                    onMessage: async statusEvent =>
                    {
                        await _semaphore.WaitAsync(stoppingToken);

                        try
                        {
                            using var serviceScope = _serviceScopeFactory.CreateScope();

                            var applicationDbContext =
                                serviceScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                            var subscriptionCheck =
                                await applicationDbContext.SubmissionChecks.FirstOrDefaultAsync(
                                    s => s.Id == statusEvent.SubmissionId, cancellationToken: stoppingToken);
                            if (subscriptionCheck == null)
                                throw new InvalidOperationException();

                            if (subscriptionCheck.LastStatusVersion > statusEvent.Version)
                                return;

                            subscriptionCheck.LastSerializedStatus = statusEvent.SerializedStatus;
                            subscriptionCheck.LastStatusVersion = statusEvent.Version;

                            await applicationDbContext.SaveChangesAsync(stoppingToken);

                            await _moodleCommunicator.CallFunctionAsync(
                                functionName: "local_qtype_set_submission_status",
                                functionParams: new Dictionary<string, object>
                                {
                                    ["id"] = subscriptionCheck.AttemptId
                                },
                                requestParams: new Dictionary<string, string>
                                {
                                    ["status"] = statusEvent.SerializedStatus
                                },
                                cancellationToken: stoppingToken);
                        }
                        catch (Exception e)
                        {
                            SentrySdk.CaptureException(e);

                            await rabbitConnection
                                .Scheduler
                                .FuturePublishAsync(statusEvent, TimeSpan.FromMinutes(1), stoppingToken);
                        }
                        finally
                        {
                            _semaphore.Release();
                        }
                    });
        }
    }
}