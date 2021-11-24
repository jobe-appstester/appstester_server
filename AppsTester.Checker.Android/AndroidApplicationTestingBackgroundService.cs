using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AppsTester.Checker.Android.Adb;
using AppsTester.Shared;
using AppsTester.Shared.RabbitMq;
using EasyNetQ;
using Microsoft.Extensions.Hosting;
using SharpAdbClient;
using SharpAdbClient.Exceptions;

namespace AppsTester.Checker.Android
{
    internal class AndroidApplicationTestingBackgroundService : BackgroundService
    {
        private readonly IRabbitBusProvider _rabbitBusProvider;
        private readonly AndroidApplicationTester _androidApplicationTester;
        private readonly IAdbDevicesProvider _adbDevicesProvider;

        private readonly HashSet<string> _activeDeviceSerials = new();

        public AndroidApplicationTestingBackgroundService(
            AndroidApplicationTester androidApplicationTester,
            IRabbitBusProvider rabbitBusProvider,
            IAdbDevicesProvider adbDevicesProvider)
        {
            _androidApplicationTester = androidApplicationTester;
            _rabbitBusProvider = rabbitBusProvider;
            _adbDevicesProvider = adbDevicesProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var devices = _adbDevicesProvider.GetOnlineDevices();

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
            using var rabbitConnection = _rabbitBusProvider.GetRabbitBus();

            var cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);

            var subscriptionResult = await rabbitConnection
                .PubSub
                .SubscribeAsync<SubmissionCheckRequest>(
                    subscriptionId: "", 
                    onMessage: async request =>
                    {
                        try
                        {
                            var result =
                                await _androidApplicationTester.CheckSubmissionAsync(request, deviceData, stoppingToken);
                            await rabbitConnection.PubSub.PublishAsync(result, "", stoppingToken);
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
                    }
                );

            cancellationTokenSource.Token.Register(() => subscriptionResult?.Dispose());

            while (!stoppingToken.IsCancellationRequested && !cancellationTokenSource.IsCancellationRequested)
                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
        }
    }
}