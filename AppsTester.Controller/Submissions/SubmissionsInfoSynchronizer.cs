using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using AppsTester.Controller.Files;
using AppsTester.Shared.Events;
using AppsTester.Shared.RabbitMq;
using EasyNetQ;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;

namespace AppsTester.Controller.Submissions
{
    public class SubmissionsInfoSynchronizer : BackgroundService
    {
        private readonly IConfiguration _configuration;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly IRabbitBusProvider _rabbitBusProvider;

        public SubmissionsInfoSynchronizer(
            IConfiguration configuration,
            IServiceScopeFactory serviceScopeFactory,
            IRabbitBusProvider rabbitBusProvider)
        {
            _configuration = configuration;
            _serviceScopeFactory = serviceScopeFactory;
            _rabbitBusProvider = rabbitBusProvider;
        }
        
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (true)
            {
                var httpClient = new HttpClient();
                
                var attemptIds = await httpClient.GetFromJsonAsync<int[]>(
                   $"{_configuration["Moodle:Url"]}/webservice/rest/server.php?wstoken={_configuration["Moodle:Token"]}&wsfunction=local_qtype_get_submissions_to_check&moodlewsrestformat=json", cancellationToken: stoppingToken);

                if (attemptIds == null || !attemptIds.Any())
                {
                    await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
                    continue;
                }

                using var serviceScope = _serviceScopeFactory.CreateScope();
                await using var dbContext = serviceScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                foreach (var attemptId in attemptIds)
                {
                    if (dbContext.SubmissionChecks.Any(sc => sc.AttemptId == attemptId))
                    {
                        await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
                        continue;
                    }

                    var submissionString = await httpClient.GetStringAsync(
                        $"{_configuration["Moodle:Url"]}/webservice/rest/server.php?wstoken={_configuration["Moodle:Token"]}&wsfunction=local_qtype_get_submission&moodlewsrestformat=json&id={attemptId}",
                        stoppingToken);

                    var submission = JsonConvert.DeserializeObject<Submission>(submissionString);
                    if (submission == null)
                        continue;

                    var fileCache = serviceScope.ServiceProvider.GetRequiredService<FileCache>();

                    var missingFiles = submission
                        .Files
                        .Where(pair => pair.Key.EndsWith("_hash"))
                        .Where(pair => !fileCache.IsKeyExists(pair.Value))
                        .ToList();

                    if (missingFiles.Any())
                    {
                        submissionString = await httpClient.GetStringAsync(
                            $"{_configuration["Moodle:Url"]}/webservice/rest/server.php?wstoken={_configuration["Moodle:Token"]}&wsfunction=local_qtype_get_submission&moodlewsrestformat=json&id={attemptId}&included_file_hashes={string.Join(",", missingFiles.Select(mf => mf.Value))}",
                            stoppingToken);

                        submission = JsonConvert.DeserializeObject<Submission>(submissionString);
                        if (submission == null)
                            continue;

                        foreach (var (fileName, fileHash) in missingFiles)
                            fileCache.Write(fileHash,
                                Convert.FromBase64String(submission.Files[fileName.Substring(0, fileName.Length - 5)]));
                    }

                    var submissionId = Guid.NewGuid();
                    var submissionCheckRequest = new SubmissionCheckRequestEvent
                    {
                        SubmissionId = submissionId,
                        Files = submission.Files.Where(f => f.Key.EndsWith("_hash"))
                            .ToDictionary(pair => pair.Key.Substring(0, pair.Key.Length - 5), pair => pair.Value),
                        PlainParameters = submission.Parameters
                    };
                    var submissionCheck = new SubmissionCheck
                    {
                        Id = submissionId,
                        AttemptId = attemptId,
                        SendingDateTimeUtc = DateTime.UtcNow
                    };
                    dbContext.SubmissionChecks.Add(submissionCheck);

                    var rabbitConnection = _rabbitBusProvider.GetRabbitBus();
                    await rabbitConnection.PubSub.PublishAsync(submissionCheckRequest, topic: submission.CheckerSystemName,
                        cancellationToken: stoppingToken);

                    await dbContext.SaveChangesAsync(stoppingToken);
                }
                
                await Task.Delay(TimeSpan.FromSeconds(1));
            }
        }
    }
}