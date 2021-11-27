using System;
using Medallion.Threading;
using SharpAdbClient;

namespace AppsTester.Checker.Android.Devices
{
    internal interface IReservedDevice : IDisposable
    {
        DeviceData DeviceData { get; }
    }

    internal class ReservedDevice : IReservedDevice
    {
        private readonly IDistributedSynchronizationHandle _distributedSynchronizationHandle;

        public DeviceData DeviceData { get; }

        public ReservedDevice(DeviceData deviceData, IDistributedSynchronizationHandle distributedSynchronizationHandle)
        {
            DeviceData = deviceData;
            _distributedSynchronizationHandle = distributedSynchronizationHandle;
        }

        public void Dispose()
        {
            _distributedSynchronizationHandle.Dispose();
        }
    }
}