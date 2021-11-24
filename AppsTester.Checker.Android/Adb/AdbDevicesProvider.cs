using System.Collections.Generic;
using System.Linq;
using SharpAdbClient;

namespace AppsTester.Checker.Android.Adb
{
    internal interface IAdbDevicesProvider
    {
        IEnumerable<DeviceData> GetOnlineDevices();
    }
    
    internal class AdbDevicesProvider : IAdbDevicesProvider
    {
        private readonly IAdbClientProvider _adbClientProvider;

        public AdbDevicesProvider(IAdbClientProvider adbClientProvider)
        {
            _adbClientProvider = adbClientProvider;
        }

        public IEnumerable<DeviceData> GetOnlineDevices()
        {
            var adbClient = _adbClientProvider.GetAdbClient();

            return adbClient
                .GetDevices()
                .Where(device => device.State == DeviceState.Online)
                .ToList();
        }
    }
}