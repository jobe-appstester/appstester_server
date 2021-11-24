using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using AppsTester.Shared;
using EasyNetQ;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;

namespace AppsTester.Controller.Submissions
{
    public class SubscriptionCheckStatusesProcessor : BackgroundService
    {
        private readonly IConfiguration _configuration;
        private readonly IServiceScopeFactory _serviceScopeFactory;

        public SubscriptionCheckStatusesProcessor(IConfiguration configuration, IServiceScopeFactory serviceScopeFactory)
        {
            _configuration = configuration;
            _serviceScopeFactory = serviceScopeFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var rabbitConnection =
                RabbitHutch.CreateBus($"host={_configuration["Rabbit:Host"]};port=5672;prefetchcount=1;username={_configuration["Rabbit:Username"]};password={_configuration["Rabbit:Password"]}");

            await rabbitConnection.SendReceive.ReceiveAsync<SubmissionCheckStatusEvent>("Checker.Android.Status", async statusEvent =>
            {
                try
                {
                    using var serviceScope = _serviceScopeFactory.CreateScope();
                    
                    var applicationDbContext = serviceScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    
                    var subscriptionCheck = await applicationDbContext.SubmissionChecks.FirstOrDefaultAsync(s => s.Id == statusEvent.SubmissionId, cancellationToken: stoppingToken);
                    if (subscriptionCheck == null)
                        throw new InvalidOperationException();

                    subscriptionCheck.SubmissionCheckStatus = statusEvent.SerializedStatus;
                    await applicationDbContext.SaveChangesAsync(stoppingToken);

                    var httpClient = new HttpClient();

                    var content = new FormUrlEncodedContent(new[]
                    {
                        new KeyValuePair<string, string>("status", statusEvent.SerializedStatus),
                    });
                    
                    var response = await httpClient.PostAsync($"{_configuration["Moodle:Url"]}/webservice/rest/server.php?wstoken={_configuration["Moodle:Token"]}&wsfunction=local_qtype_set_submission_status&moodlewsrestformat=json&id={subscriptionCheck.MoodleId}", content, stoppingToken);
                    var responseContent = await response.Content.ReadAsStringAsync();
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    await rabbitConnection.Scheduler.FuturePublishAsync(statusEvent, TimeSpan.FromMinutes(1), stoppingToken);
                }
            });
        }
    }
}