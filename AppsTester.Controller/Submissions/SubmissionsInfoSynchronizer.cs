using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using AppsTester.Controller.Files;
using AppsTester.Shared;
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

        public SubmissionsInfoSynchronizer(IConfiguration configuration, IServiceScopeFactory serviceScopeFactory)
        {
            _configuration = configuration;
            _serviceScopeFactory = serviceScopeFactory;
        }
        
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (true)
            {
                var httpClient = new HttpClient();
                
                var submissions = await httpClient.GetFromJsonAsync<int[]>(
                   $"{_configuration["Moodle:Url"]}/webservice/rest/server.php?wstoken={_configuration["Moodle:Token"]}&wsfunction=local_qtype_get_submissions_to_check&moodlewsrestformat=json", cancellationToken: stoppingToken);

                if (submissions == null || !submissions.Any())
                {
                    await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
                    continue;
                }

                using var serviceScope = _serviceScopeFactory.CreateScope();
                await using var dbContext = serviceScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                if (dbContext.SubmissionChecks.Any(sc => sc.MoodleId == submissions.First()))
                {
                    await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
                    continue;
                }

                var submissionString = await httpClient.GetStringAsync(
                   $"{_configuration["Moodle:Url"]}/webservice/rest/server.php?wstoken={_configuration["Moodle:Token"]}&wsfunction=local_qtype_get_submission&moodlewsrestformat=json&id={submissions.First()}", stoppingToken);

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
                       $"{_configuration["Moodle:Url"]}/webservice/rest/server.php?wstoken={_configuration["Moodle:Token"]}&wsfunction=local_qtype_get_submission&moodlewsrestformat=json&id={submissions.First()}&included_file_hashes={string.Join(",", missingFiles.Select(mf => mf.Value))}", stoppingToken);

                    submission = JsonConvert.DeserializeObject<Submission>(submissionString);
                    if (submission == null)
                        continue;

                    foreach (var (fileName, fileHash) in missingFiles)
                        fileCache.Write(fileHash, Convert.FromBase64String(submission.Files[fileName.Substring(0, fileName.Length - 5)]));
                }
                
                var id = Guid.NewGuid();
                var submissionCheckRequest = new SubmissionCheckRequest
                {
                    Id = id,
                    Files = submission.Files.Where(f => f.Key.EndsWith("_hash")).ToDictionary(pair => pair.Key.Substring(0, pair.Key.Length - 5), pair => pair.Value),
                    Parameters = submission.Parameters
                };
                var submissionCheck = new SubmissionCheck
                {
                    Id = id,
                    MoodleId = submissions.First(),
                    SubmissionCheckRequest = submissionCheckRequest,
                    SendingDateTimeUtc = DateTime.UtcNow
                };
                dbContext.SubmissionChecks.Add(submissionCheck);
                await dbContext.SaveChangesAsync(stoppingToken);
                
                using var rabbitConnection =
                    RabbitHutch.CreateBus($"host={_configuration["Rabbit:Host"]};port=5672;prefetchcount=1;username={_configuration["Rabbit:Username"]};password={_configuration["Rabbit:Password"]}");
                await rabbitConnection.PubSub.PublishAsync(submissionCheckRequest, "", cancellationToken: stoppingToken);
                
                await Task.Delay(TimeSpan.FromSeconds(1));
            }
        }
    }
}