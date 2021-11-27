using System.Threading;
using System.Threading.Tasks;
using AppsTester.Checker.Android.Adb;
using Medallion.Threading;

namespace AppsTester.Checker.Android.Devices
{
    internal interface IReservedDevicesProvider
    {
        Task<IReservedDevice> ReserveDeviceAsync(CancellationToken cancellationToken);
    }

    internal class ReservedDevicesProvider : IReservedDevicesProvider
    {
        private readonly IAdbDevicesProvider _adbDevicesProvider;
        private readonly IDistributedLockProvider _distributedLockProvider;

        public ReservedDevicesProvider(
            IAdbDevicesProvider adbDevicesProvider, IDistributedLockProvider distributedLockProvider)
        {
            _adbDevicesProvider = adbDevicesProvider;
            _distributedLockProvider = distributedLockProvider;
        }

        public async Task<IReservedDevice> ReserveDeviceAsync(CancellationToken cancellationToken)
        {
            while (true)
            {
                var onlineDevices = _adbDevicesProvider.GetOnlineDevices();

                foreach (var onlineDevice in onlineDevices)
                {
                    var distributedSynchronizationHandle = await _distributedLockProvider
                        .TryAcquireLockAsync(onlineDevice.Serial, cancellationToken: cancellationToken);

                    if (distributedSynchronizationHandle == null)
                        continue;

                    return new ReservedDevice(onlineDevice, distributedSynchronizationHandle);
                }

                cancellationToken.ThrowIfCancellationRequested();
            }
        }
    }
}