using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AppsTester.Controller.Files;
using AppsTester.Controller.Services;
using AppsTester.Controller.Services.Moodle;
using AppsTester.Shared.RabbitMq;
using AppsTester.Shared.SubmissionChecker.Events;
using AppsTester.Shared.SubmissionChecker.Results;
using AppsTester.Shared.SubmissionChecker.Statuses;
using EasyNetQ;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace AppsTester.Controller.Submissions
{
    internal class SubmissionsInfoSynchronizer : BackgroundService
    {
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly IRabbitBusProvider _rabbitBusProvider;
        private readonly IMoodleService _moodleService;
        private readonly ILogger<SubmissionsInfoSynchronizer> _logger;


        public SubmissionsInfoSynchronizer(
            IServiceScopeFactory serviceScopeFactory,
            IRabbitBusProvider rabbitBusProvider,
            IMoodleService moodleService,
            ILogger<SubmissionsInfoSynchronizer> logger)
        {
            _serviceScopeFactory = serviceScopeFactory;
            _rabbitBusProvider = rabbitBusProvider;
            _moodleService = moodleService;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (true)
            {
                try
                {
                    var submissionsUnits = await _moodleService.GetSubmissionsToCheckAsync(stoppingToken);
                    if (submissionsUnits == null || !submissionsUnits.Any())
                    {
                        await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
                        continue;
                    }

                    using var serviceScope = _serviceScopeFactory.CreateScope();
                    await using var dbContext = serviceScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                    foreach (var submissionUnit in submissionsUnits)
                    {
                        var attemptId = submissionUnit.AttemptId;
                        var attemptStepIds = submissionUnit.AttemptStepsIds;
                        var latestCheckDateTime = await dbContext.SubmissionChecks
                            .Where(x => x.AttemptId == attemptId)
                            .Select(x => x.SendingDateTimeUtc)
                            .OrderByDescending(x => x)
                            .FirstOrDefaultAsync(stoppingToken);
                        foreach (var attemptStepId in attemptStepIds)
                        {
                            try
                            {
                                var submissionCheckRecord = await dbContext.SubmissionChecks
                                    .Where(x => x.AttemptStepId == attemptStepId)
                                    .OrderByDescending(s => s.SendingDateTimeUtc)
                                    .FirstOrDefaultAsync(stoppingToken);
                                // updating AttemptId field after migrations
                                if (submissionCheckRecord != null && submissionCheckRecord.AttemptId == 0)
                                {
                                    if (latestCheckDateTime == DateTime.MinValue)
                                    {
                                        latestCheckDateTime = submissionCheckRecord.SendingDateTimeUtc;
                                    }
                                    submissionCheckRecord.AttemptId = attemptId;
                                    await dbContext.SaveChangesAsync(stoppingToken);
                                }

                                // if there is a record for given stepID in DB, and it isn't latest sent submission
                                if (submissionCheckRecord != null && submissionCheckRecord.SendingDateTimeUtc != latestCheckDateTime)
                                {
                                    await _moodleService.SetSubmissionStatusAsync(
                                        submissionCheckRecord.AttemptStepId, 
                                        submissionCheckRecord.LastSerializedStatus, 
                                        stoppingToken);
                                    await _moodleService.SetSubmissionResultAsync(
                                        submissionCheckRecord.AttemptStepId, 
                                        submissionCheckRecord.SerializedResult,
                                        stoppingToken);
                                }
                                // if this submission was never checked before
                                // OR if it's latest sent submission with a result (sent back to be regraded)
                                else if (submissionCheckRecord == null || 
                                         submissionCheckRecord.SendingDateTimeUtc == latestCheckDateTime && submissionCheckRecord.SerializedResult != null)
                                {
                                    var submission = await _moodleService.GetSubmissionAsync(attemptStepId, stoppingToken);

                                    if (submission.AttemptId == 0) // moodle couldn't retrieve submission properly
                                    {
                                        await _moodleService.SetSubmissionStatusAsync(
                                            attemptStepId, 
                                            JsonConvert.SerializeObject(new ProcessingStatus("get_submission_failed")),
                                            stoppingToken);
                                        await _moodleService.SetSubmissionResultAsync(
                                            attemptStepId,
                                            JsonConvert.SerializeObject(new ValidationErrorResult(ValidationError: "Can't download submission file.")),
                                            stoppingToken);
                                    }
                                    else
                                    {
                                        var fileCache = serviceScope.ServiceProvider.GetRequiredService<FileCache>();

                                        var missingFiles = submission
                                            .Files
                                            .Where(pair => pair.Key.EndsWith("_hash"))
                                            .Where(pair => !fileCache.IsKeyExists(pair.Value))
                                            .ToList();
                                        
                                        if (missingFiles.Any())
                                        {
                                            submission = await _moodleService.GetSubmissionAsync(attemptStepId, stoppingToken,
                                                string.Join(",",
                                                    missingFiles.Select(mf => mf.Value)));
    
                                            foreach (var (fileName, fileHash) in missingFiles)
                                                fileCache.Write(fileHash,
                                                    Convert.FromBase64String(
                                                        submission.Files[fileName.Substring(0, fileName.Length - 5)]));
                                        }
                                        
                                        var submissionId = Guid.NewGuid();
                                        var submissionCheckRequest = new SubmissionCheckRequestEvent
                                        {
                                            SubmissionId = submissionId,
                                            Files = submission.Files.Where(f => f.Key.EndsWith("_hash"))
                                                .ToDictionary(pair => pair.Key.Substring(0, pair.Key.Length - 5),
                                                    pair => pair.Value),
                                            PlainParameters = submission.Parameters
                                        };
                                        var submissionCheck = new SubmissionCheck
                                        {
                                            Id = submissionId,
                                            AttemptId = attemptId,
                                            AttemptStepId = attemptStepId,
                                            SendingDateTimeUtc = DateTime.UtcNow,
                                            SerializedRequest = JsonConvert.SerializeObject(submissionCheckRequest)
                                        };
                                        dbContext.SubmissionChecks.Add(submissionCheck);
                                        
                                        var rabbitConnection = _rabbitBusProvider.GetRabbitBus();
                                        await rabbitConnection.PubSub.PublishAsync(submissionCheckRequest,
                                            topic: submission.CheckerSystemName,
                                            cancellationToken: stoppingToken);
    
                                        await dbContext.SaveChangesAsync(stoppingToken);
                                    }
                                }
                            }
                            catch (Exception e)
                            {
                                _logger.LogError(e, "can't handle attemptStep with ID {attemptStepId}", attemptStepId);
                            }
                            finally
                            {
                                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
                            }
                        }                        
                    }
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "unhandled exception");
                }
                finally
                {
                    await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
                }
            }
        }
    }
}