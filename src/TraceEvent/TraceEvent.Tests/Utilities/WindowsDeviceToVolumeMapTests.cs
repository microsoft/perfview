using System;
using Microsoft.Diagnostics.Utilities;
using Xunit;
using Xunit.Abstractions;

namespace TraceEventTests
{
    public class WindowsDeviceToVolumeMapTests
    {
        private readonly ITestOutputHelper _output;

        public WindowsDeviceToVolumeMapTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [WindowsFact]
        public void WindowsDeviceToVolumeMap_Initialize_DoesNotThrow()
        {
            // Test that initialization doesn't throw
            WindowsDeviceToVolumeMap map = null;
            Exception exception = Record.Exception(() => map = new WindowsDeviceToVolumeMap());
            
            Assert.Null(exception);
            Assert.NotNull(map);
            
            _output.WriteLine("WindowsDeviceToVolumeMap initialized successfully");
        }

        [WindowsFact]
        public void WindowsDeviceToVolumeMap_ConvertDevicePathToVolumePath_DoesNotThrow()
        {
            WindowsDeviceToVolumeMap map = new WindowsDeviceToVolumeMap();
            
            // Test with some example device paths that might exist
            string[] testPaths = new[]
            {
                @"\Device\HarddiskVolume1\Windows\System32\kernel32.dll",
                @"\Device\HarddiskVolume2\Program Files\test.exe",
                @"\\Device\VhdHardDisk{12345678-1234-1234-1234-123456789abc}\test\file.txt",
                @"C:\Windows\System32\kernel32.dll", // Regular path should pass through unchanged
                @"", // Empty string
                @"\SomeOtherDevicePath\file.txt" // Non-matching device path
            };

            foreach (string testPath in testPaths)
            {
                Exception exception = Record.Exception(() =>
                {
                    string result = map.ConvertDevicePathToVolumePath(testPath);
                    _output.WriteLine($"Input: '{testPath}' -> Output: '{result}'");
                });
                
                Assert.Null(exception);
            }
        }

        [WindowsFact]
        public void WindowsDeviceToVolumeMap_ShowDeviceToVolumeMapping()
        {
            WindowsDeviceToVolumeMap map = new WindowsDeviceToVolumeMap();

            // Use reflection to access the private _deviceNameToVolumeNameMap field
            var mapField = typeof(WindowsDeviceToVolumeMap).GetField("_deviceNameToVolumeNameMap", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            Assert.NotNull(mapField);
            
            var deviceToVolumeMap = mapField.GetValue(map) as System.Collections.Generic.Dictionary<string, string>;
            Assert.NotNull(deviceToVolumeMap);

            _output.WriteLine($"Found {deviceToVolumeMap.Count} device-to-volume mappings:");
            
            foreach (var kvp in deviceToVolumeMap)
            {
                _output.WriteLine($"  Device: '{kvp.Key}' -> Volume: '{kvp.Value}'");
            }

            // The map should contain at least one mapping on a typical Windows system
            // but we can't assert this since it depends on the system configuration
            _output.WriteLine($"Total mappings found: {deviceToVolumeMap.Count}");
        }
    }
}