using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace Microsoft.Diagnostics.Utilities
{
    public sealed class WindowsDeviceToVolumeMap
    {
        private static bool s_LegacyPathHandlingDisabled = false;

        private const string VolumeNamePrefix = @"\\?\";
        private const string VolumeNameSuffix = "\\";

        private const string HardDiskVolumePathToken = "\\Device\\HarddiskVolume";
        private const int HardDiskVolumePathTokenIndex = 0;
        private const string VHDHardDiskPathToken = "Device\\VhdHardDisk{";
        private const int VHDHardDiskPathTokenIndex = 3;

        private Dictionary<string, string> _deviceNameToVolumeNameMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public WindowsDeviceToVolumeMap()
        {
            Initialize();
        }

        /// <summary>
        /// Convert an input file path from a device path to a volume-based path.
        /// Example input: \device\harddiskvolume7\sdk\shared\microsoft.netcore.app\3.1.7\coreclr.dll
        /// Example output: \\?\Volume{a296af82-8d67-4f46-a792-9e78ec0adf9b}\sdk\shared\microsoft.netcore.app\3.1.7\coreclr.dll
        /// </summary>
        public string ConvertDevicePathToVolumePath(string inputPath)
        {
            string updatedPath = inputPath;
            bool shouldDisableLegacyPathHandling = false;

            // Check for "Device\HarddiskVolume" pattern.
            if (inputPath.IndexOf(HardDiskVolumePathToken, 0, StringComparison.OrdinalIgnoreCase) == HardDiskVolumePathTokenIndex)
            {
                // Get the device path and see if we have a match.
                int indexOfSlashAfterDeviceName = inputPath.IndexOf("\\", HardDiskVolumePathTokenIndex + HardDiskVolumePathToken.Length, StringComparison.OrdinalIgnoreCase);
                string deviceName = inputPath.Substring(0, indexOfSlashAfterDeviceName);

                string volumePath;
                if (_deviceNameToVolumeNameMap.TryGetValue(deviceName, out volumePath))
                {
                    // Get the rest of the path.
                    string restOfPath = inputPath.Substring(indexOfSlashAfterDeviceName + 1);

                    // Replace the device path with the volume path.
                    updatedPath = Path.Combine(volumePath, restOfPath);

                    // Disable legacy path handling.
                    shouldDisableLegacyPathHandling = true;
                }
            }
            // Check for "Device\VhdHardDisk{GUID}" pattern.
            else if (inputPath.IndexOf(VHDHardDiskPathToken, 0, StringComparison.OrdinalIgnoreCase) == VHDHardDiskPathTokenIndex)
            {
                // Get the device path and see if we have a match.
                int indexOfSlashAfterDeviceName = inputPath.IndexOf("\\", VHDHardDiskPathTokenIndex + VHDHardDiskPathToken.Length, StringComparison.OrdinalIgnoreCase);
                string deviceName = inputPath.Substring(2, indexOfSlashAfterDeviceName - 2);

                string volumePath;
                if (_deviceNameToVolumeNameMap.TryGetValue(deviceName, out volumePath))
                {
                    // Get the rest of the path.
                    string restOfPath = inputPath.Substring(indexOfSlashAfterDeviceName + 1);

                    // Replace the device path with the volume path.
                    updatedPath = Path.Combine(volumePath, restOfPath);

                    // Disable legacy path handling.
                    shouldDisableLegacyPathHandling = true;
                }
            }

            if(!s_LegacyPathHandlingDisabled && shouldDisableLegacyPathHandling)
            {
                DisableLegacyPathHandling();
            }

            return updatedPath;
        }

        private void Initialize()
        {
            // Create a string builder which will act as the buffer used to receive the volume and device names.
            StringBuilder builder = new StringBuilder((int)Interop.MAX_PATH, (int)Interop.MAX_PATH);

            // Get the first volume.
            IntPtr findHandle = Interop.FindFirstVolume(builder, Interop.MAX_PATH);
            try
            {
                do
                {
                    string volumeName = builder.ToString();
                    string deviceName = string.Empty;
                    string lookupKey = volumeName;

                    // Strip off the volume name prefix and suffix.
                    if (lookupKey.StartsWith(VolumeNamePrefix))
                    {
                        lookupKey = lookupKey.Substring(VolumeNamePrefix.Length);
                    }
                    if (lookupKey.EndsWith(VolumeNameSuffix))
                    {
                        lookupKey = lookupKey.Substring(0, lookupKey.Length - VolumeNameSuffix.Length);
                    }

                    // Get the device name.
                    uint charsWritten = Interop.QueryDosDevice(lookupKey, builder, (int)Interop.MAX_PATH);
                    if (charsWritten > 0)
                    {
                        deviceName = builder.ToString();
                    }

                    // Save the mapping.
                    if (volumeName.Length > 0 && deviceName.Length > 0)
                    {
                        _deviceNameToVolumeNameMap.Add(deviceName, volumeName);
                    }
                }
                while (Interop.FindNextVolume(findHandle, builder, Interop.MAX_PATH));
            }
            finally
            {
                // Close the live volume handle.
                if (findHandle != IntPtr.Zero)
                {
                    Interop.FindVolumeClose(findHandle);
                }
            }
        }

        /// <summary>
        /// Disable the AppContextSwitch "UseLegacyPathHandling", as legacy path handling doesn't support usage of volume-based paths.
        /// This is done via reflection because PerfView and TraceEvent target .NET 4.5 which doesn't have access to AppContext.  If this is
        /// run on a .NET 4.5 runtime, it will silently fail, but it's unlikely that anyone that needs this functionality for container support
        /// is going to be running .NET 4.5.
        /// </summary>
        private static void DisableLegacyPathHandling()
        {
            AssemblyName assemblyName = new AssemblyName("mscorlib");
            Assembly systemAssembly = Assembly.Load(assemblyName);
            if (systemAssembly != null)
            {
                // Disable the AppContext switch.
                Type appContextType = systemAssembly.GetType("System.AppContext");
                if (appContextType != null)
                {
                    MethodInfo setSwitchMethod = appContextType.GetMethod("SetSwitch");
                    if (setSwitchMethod != null)
                    {
                        setSwitchMethod.Invoke(null, new object[] { "Switch.System.IO.UseLegacyPathHandling", false });
                    }
                }

                // Invalidate the cached copy of the switch.
                Type appContextSwitchesType = systemAssembly.GetType("System.AppContextSwitches");
                if (appContextSwitchesType != null)
                {
                    FieldInfo useLegacyPathHandlingField = appContextSwitchesType.GetField("_useLegacyPathHandling", BindingFlags.NonPublic | BindingFlags.Static);
                    if (useLegacyPathHandlingField != null)
                    {
                        useLegacyPathHandlingField.SetValue(0, 0);
                    }
                }
            }
        }
    }

    internal static class Interop
    {
        public const UInt32 MAX_PATH = 1024;

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern IntPtr FindFirstVolume(
            [Out] StringBuilder lpszVolumeName,
            uint cchBufferLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool FindNextVolume(
            IntPtr hFindVolume,
            [Out] StringBuilder lpszVolumeName,
            uint cchBufferLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool FindVolumeClose(
            IntPtr hFindVolume);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern uint QueryDosDevice(
            string lpDeviceName,
            StringBuilder lpTargetPath,
            int ucchMax);
    }
}
