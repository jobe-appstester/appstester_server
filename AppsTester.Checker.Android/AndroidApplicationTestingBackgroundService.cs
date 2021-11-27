using System;
using System.Threading;
using System.Threading.Tasks;
using AppsTester.Checker.Android.Devices;
using AppsTester.Shared.Events;
using AppsTester.Shared.RabbitMq;
using EasyNetQ;
using Microsoft.Extensions.Hosting;
using SharpAdbClient.Exceptions;

namespace AppsTester.Checker.Android
{
    internal class AndroidApplicationTestingBackgroundService : BackgroundService
    {
        private readonly IRabbitBusProvider _rabbitBusProvider;
        private readonly AndroidApplicationTester _androidApplicationTester;
        private readonly IReservedDevicesProvider _reservedDevicesProvider;

        public AndroidApplicationTestingBackgroundService(
            AndroidApplicationTester androidApplicationTester,
            IRabbitBusProvider rabbitBusProvider, IReservedDevicesProvider reservedDevicesProvider)
        {
            _androidApplicationTester = androidApplicationTester;
            _rabbitBusProvider = rabbitBusProvider;
            _reservedDevicesProvider = reservedDevicesProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var rabbitConnection = _rabbitBusProvider.GetRabbitBus();

            await rabbitConnection
                .PubSub
                .SubscribeAsync<SubmissionCheckRequestEvent>(
                    subscriptionId: "android",
                    onMessage: async request =>
                    {
                        try
                        {
                            using var reservedDevice = await _reservedDevicesProvider.ReserveDeviceAsync(stoppingToken);

                            var result = await _androidApplicationTester.CheckSubmissionAsync(
                                request, reservedDevice.DeviceData, stoppingToken);

                            await rabbitConnection.PubSub.PublishAsync(result, "", stoppingToken);
                        }
                        catch (AdbException e) when (e.Message == "Device is offline")
                        {
                            await rabbitConnection.Scheduler.FuturePublishAsync(
                                request, delay: TimeSpan.FromMinutes(1), cancellationToken: stoppingToken);
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e);

                            await rabbitConnection.Scheduler.FuturePublishAsync(
                                request, delay: TimeSpan.FromMinutes(1), cancellationToken: stoppingToken);
                        }
                    },
                    configure: configuration => configuration.WithTopic("android"),
                    cancellationToken: stoppingToken
                );

            await Task.Delay(millisecondsDelay: -1, stoppingToken);
        }
    }
}