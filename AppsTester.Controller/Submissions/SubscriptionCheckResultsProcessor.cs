using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using AppsTester.Shared.Events;
using AppsTester.Shared.RabbitMq;
using EasyNetQ;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AppsTester.Controller.Submissions
{
    public class SubscriptionCheckResultsProcessor : BackgroundService
    {
        private readonly IConfiguration _configuration;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly IRabbitBusProvider _rabbitBusProvider;

        public SubscriptionCheckResultsProcessor(
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
            var rabbitConnection = _rabbitBusProvider.GetRabbitBus();

            await rabbitConnection.PubSub.SubscribeAsync<SubmissionCheckResultEvent>("", async resultEvent =>
            {
                try
                {
                    using var serviceScope = _serviceScopeFactory.CreateScope();
                    
                    var applicationDbContext = serviceScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    
                    var subscriptionCheck = await applicationDbContext.SubmissionChecks.FirstOrDefaultAsync(s => s.Id == resultEvent.SubmissionId, cancellationToken: stoppingToken);
                    if (subscriptionCheck == null)
                        throw new InvalidOperationException();

                    await applicationDbContext.SaveChangesAsync(stoppingToken);

                    var httpClient = new HttpClient();

                    var content = new FormUrlEncodedContent(new[]
                    {
                        new KeyValuePair<string, string>("result", resultEvent.SerializedResult),
                    });
                    
                    var response = await httpClient.PostAsync($"{_configuration["Moodle:Url"]}/webservice/rest/server.php?wstoken={_configuration["Moodle:Token"]}&wsfunction=local_qtype_set_submission_results&moodlewsrestformat=json&id={subscriptionCheck.AttemptId}", content, stoppingToken);
                    var responseContent = await response.Content.ReadAsStringAsync();
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    await rabbitConnection.Scheduler.FuturePublishAsync(resultEvent, TimeSpan.FromMinutes(1), stoppingToken);
                }
            });
        }
    }
}