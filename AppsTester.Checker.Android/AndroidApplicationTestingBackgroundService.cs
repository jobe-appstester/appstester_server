using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AppsTester.Shared;
using EasyNetQ;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using SharpAdbClient;
using SharpAdbClient.Exceptions;

namespace AppsTester.Checker.Android
{
    internal class AndroidApplicationTestingBackgroundService : BackgroundService
    {
        private readonly AndroidApplicationTester _androidApplicationTester;
        private readonly IConfiguration _configuration;

        private readonly HashSet<string> _activeDeviceSerials = new();

        public AndroidApplicationTestingBackgroundService(AndroidApplicationTester androidApplicationTester,
            IConfiguration configuration)
        {
            _androidApplicationTester = androidApplicationTester;
            _configuration = configuration;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var devices = _androidApplicationTester.GetOnlineDevices();

                foreach (var deviceData in devices.Where(d => !_activeDeviceSerials.Contains(d.Serial)))
                {
                    _activeDeviceSerials.Add(deviceData.Serial);
                    CheckSubmissionsAsync(deviceData, stoppingToken);
                }

                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }

        private async Task CheckSubmissionsAsync(DeviceData deviceData, CancellationToken stoppingToken)
        {
            using var rabbitConnection = 
                RabbitHutch.CreateBus($"host={_configuration["Rabbit:Host"]};port=5672;prefetchcount=1;username={_configuration["Rabbit:Username"]};password={_configuration["Rabbit:Password"]}");

            var cancellationTokenSource = new CancellationTokenSource();
            stoppingToken.Register(() => cancellationTokenSource.Cancel());
            
            var subscriptionResult = await rabbitConnection.PubSub.SubscribeAsync<SubmissionCheckRequest>(
                "submission_requests", async request =>
                {
                    try
                    {
                        var result =
                            await _androidApplicationTester.CheckSubmissionAsync(request, deviceData, stoppingToken);
                        await rabbitConnection.PubSub.PublishAsync(result, "submission_results", stoppingToken);
                    }
                    catch (AdbException e) when (e.Message == "Device is offline")
                    {
                        _activeDeviceSerials.Remove(deviceData.Serial);
                        await rabbitConnection.Scheduler.FuturePublishAsync(request, TimeSpan.FromMinutes(1), cancellationToken: stoppingToken);
                        
                        cancellationTokenSource.Cancel();
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e);
                        await rabbitConnection.Scheduler.FuturePublishAsync(request, TimeSpan.FromMinutes(1), cancellationToken: stoppingToken);
                    }
                });

            cancellationTokenSource.Token.Register(() => subscriptionResult?.Dispose());

            while (!stoppingToken.IsCancellationRequested && !cancellationTokenSource.IsCancellationRequested)
                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
        }
    }
}