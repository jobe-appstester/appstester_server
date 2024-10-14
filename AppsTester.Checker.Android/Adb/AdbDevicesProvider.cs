using System.Collections.Generic;
using System.Linq;
using AdvancedSharpAdbClient;

namespace AppsTester.Checker.Android.Adb
{
    internal interface IAdbDevicesProvider
    {
        List<DeviceData> GetOnlineDevices();
    }
    
    internal class AdbDevicesProvider : IAdbDevicesProvider
    {
        private readonly IAdbClientProvider _adbClientProvider;

        public AdbDevicesProvider(IAdbClientProvider adbClientProvider)
        {
            _adbClientProvider = adbClientProvider;
        }

        public List<DeviceData> GetOnlineDevices()
        {
            var adbClient = _adbClientProvider.GetAdbClient();

            return adbClient
                .GetDevices()
                .Where(device => device.State == DeviceState.Online)
                .ToList();
        }
    }
}