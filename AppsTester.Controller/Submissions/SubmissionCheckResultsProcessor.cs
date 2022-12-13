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
using Microsoft.Extensions.Logging;

namespace AppsTester.Controller.Submissions
{
    internal class SubmissionCheckResultsBusReceiver : BackgroundService
    {
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly IRabbitBusProvider _rabbitBusProvider;
        private readonly ILogger<SubmissionCheckResultsBusReceiver> _logger;

        public SubmissionCheckResultsBusReceiver(
            IServiceScopeFactory serviceScopeFactory,
            IRabbitBusProvider rabbitBusProvider,
            ILogger<SubmissionCheckResultsBusReceiver> logger)
        {
            _serviceScopeFactory = serviceScopeFactory;
            _rabbitBusProvider = rabbitBusProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var rabbitConnection = _rabbitBusProvider.GetRabbitBus();

            await rabbitConnection
                .PubSub
                .SubscribeAsync<SubmissionCheckResultEvent>(subscriptionId: "", onMessage: async resultEvent =>
                    {
                        try
                        {
                            using var scope = _serviceScopeFactory.CreateScope();
                            var processor = scope.ServiceProvider.GetRequiredService<SubmissionCheckResultsProcessor>();
                            await processor.HandleResultEvent(resultEvent, stoppingToken);
                        }
                        catch (Exception e)
                        {
                            _logger.LogError(e, "can't handle submission result {SubmissionId}, retry in one minute", resultEvent.SubmissionId);

                            await rabbitConnection
                                .Scheduler
                                .FuturePublishAsync(resultEvent, TimeSpan.FromMinutes(1), stoppingToken);
                        }
                    }, cancellationToken: stoppingToken);
        }


    }

    public class SubmissionCheckResultsProcessor
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMoodleCommunicator _moodleCommunicator;

        public SubmissionCheckResultsProcessor(IUnitOfWork unitOfWork, IMoodleCommunicator moodleCommunicator)
        {
            _unitOfWork = unitOfWork;
            _moodleCommunicator = moodleCommunicator;
        }

        public async Task HandleResultEvent(SubmissionCheckResultEvent resultEvent, CancellationToken cancellationToken)
        {
            var submissionCheck = await _unitOfWork.Submissions.FindSubmissionAsync(resultEvent.SubmissionId, cancellationToken);

            if (submissionCheck == null)
                throw new InvalidOperationException($"Can't find submission {resultEvent.SubmissionId}");

            submissionCheck.SerializedResult = resultEvent.SerializedResult;
            await _unitOfWork.CompleteAsync(cancellationToken);

            await _moodleCommunicator.CallFunctionAsync(
                functionName: "local_qtype_set_submission_results",
                functionParams: new Dictionary<string, object>
                {
                    ["id"] = submissionCheck.AttemptId
                },
                requestParams: new Dictionary<string, string>
                {
                    ["result"] = resultEvent.SerializedResult
                },
                cancellationToken);
        }
    }
}