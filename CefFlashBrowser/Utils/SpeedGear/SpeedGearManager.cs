using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace CefFlashBrowser.Utils.SpeedGear
{
    internal static class SpeedGearManager
    {
        private const string SharedMappingName = "Local\\CefFlashBrowser.SpeedGear";
        private const int SharedMappingSize = 32;
        private static readonly object SyncRoot = new object();
        private static IntPtr _sharedMapping = IntPtr.Zero;
        private static IntPtr _sharedView = IntPtr.Zero;
        private static long _generation;

        public static void SetSpeedFactor(double speedFactor)
        {
            if (speedFactor <= 0 || double.IsNaN(speedFactor) || double.IsInfinity(speedFactor))
                throw new ArgumentOutOfRangeException(nameof(speedFactor));

            lock (SyncRoot)
            {
                WriteSharedSpeed(speedFactor);
                LoadLocalSpeedGearDll();
                InjectIntoCandidateProcesses();
            }
        }

        private static void WriteSharedSpeed(double speedFactor)
        {
            EnsureSharedMapping();

            Marshal.WriteInt64(_sharedView, 0, ++_generation);
            var bytes = BitConverter.GetBytes(speedFactor);
            Marshal.Copy(bytes, 0, IntPtr.Add(_sharedView, 8), bytes.Length);
        }

        private static void EnsureSharedMapping()
        {
            if (_sharedView != IntPtr.Zero)
                return;

            _sharedMapping = Win32.CreateFileMapping(new IntPtr(-1), IntPtr.Zero, Win32.PageReadWrite, 0, SharedMappingSize, SharedMappingName);
            if (_sharedMapping == IntPtr.Zero)
                throw new InvalidOperationException("CreateFileMapping failed: " + Marshal.GetLastWin32Error());

            _sharedView = Win32.MapViewOfFile(_sharedMapping, Win32.FileMapAllAccess, 0, 0, SharedMappingSize);
            if (_sharedView == IntPtr.Zero)
                throw new InvalidOperationException("MapViewOfFile failed: " + Marshal.GetLastWin32Error());
        }

        private static string GetDllPath()
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CefFlashBrowser.SpeedGear.dll");
        }

        private static void LoadLocalSpeedGearDll()
        {
            var dllPath = GetDllPath();
            if (!File.Exists(dllPath))
                throw new FileNotFoundException("Speed gear DLL not found.", dllPath);

            var module = Win32.GetModuleHandle("CefFlashBrowser.SpeedGear.dll");
            if (module == IntPtr.Zero)
            {
                module = Win32.LoadLibrary(dllPath);
                if (module == IntPtr.Zero)
                    throw new InvalidOperationException("LoadLibrary failed: " + Marshal.GetLastWin32Error());
            }
        }

        private static void InjectIntoCandidateProcesses()
        {
            var dllPath = GetDllPath();
            if (!File.Exists(dllPath))
                throw new FileNotFoundException("Speed gear DLL not found.", dllPath);

            var current = Process.GetCurrentProcess();
            var processes = Process.GetProcesses()
                .Where(p => IsCandidateProcess(p, current.Id))
                .ToArray();

            foreach (var process in processes)
            {
                try
                {
                    Inject(process.Id, dllPath);
                }
                catch (Exception e)
                {
                    LogHelper.LogError($"Failed to inject speed gear into process {process.Id}", e);
                }
                finally
                {
                    process.Dispose();
                }
            }
        }

        private static bool IsCandidateProcess(Process process, int currentProcessId)
        {
            try
            {
                if (process.Id == currentProcessId)
                    return false;

                var name = process.ProcessName;
                return name.IndexOf("CefFlashBrowser", StringComparison.OrdinalIgnoreCase) >= 0
                    || name.IndexOf("CefSharp.BrowserSubprocess", StringComparison.OrdinalIgnoreCase) >= 0;
            }
            catch
            {
                return false;
            }
        }

        private static void Inject(int processId, string dllPath)
        {
            var process = Win32.OpenProcess(Win32.ProcessCreateThread | Win32.ProcessQueryInformation |
                                            Win32.ProcessVirtualMemoryOperation | Win32.ProcessVirtualMemoryWrite |
                                            Win32.ProcessVirtualMemoryRead,
                                            false, processId);
            if (process == IntPtr.Zero)
                throw new InvalidOperationException("OpenProcess failed: " + Marshal.GetLastWin32Error());

            try
            {
                if (!IsSameArchitecture(process))
                    return;

                var bytes = Encoding.Unicode.GetBytes(dllPath + "\0");
                var remoteBuffer = Win32.VirtualAllocEx(process, IntPtr.Zero, (UIntPtr)bytes.Length, Win32.MemCommit | Win32.MemReserve, Win32.PageReadWrite);
                if (remoteBuffer == IntPtr.Zero)
                    throw new InvalidOperationException("VirtualAllocEx failed: " + Marshal.GetLastWin32Error());

                try
                {
                    if (!Win32.WriteProcessMemory(process, remoteBuffer, bytes, bytes.Length, out _))
                        throw new InvalidOperationException("WriteProcessMemory failed: " + Marshal.GetLastWin32Error());

                    var kernel32 = Win32.GetModuleHandle("kernel32.dll");
                    var loadLibrary = Win32.GetProcAddress(kernel32, "LoadLibraryW");
                    var thread = Win32.CreateRemoteThread(process, IntPtr.Zero, 0, loadLibrary, remoteBuffer, 0, IntPtr.Zero);
                    if (thread == IntPtr.Zero)
                        throw new InvalidOperationException("CreateRemoteThread failed: " + Marshal.GetLastWin32Error());

                    Win32.WaitForSingleObject(thread, 5000);
                    Win32.CloseHandle(thread);
                }
                finally
                {
                    Win32.VirtualFreeEx(process, remoteBuffer, UIntPtr.Zero, Win32.MemRelease);
                }
            }
            finally
            {
                Win32.CloseHandle(process);
            }
        }

        private static bool IsSameArchitecture(IntPtr process)
        {
            if (!Environment.Is64BitOperatingSystem)
                return true;

            if (!Win32.IsWow64Process(process, out var targetWow64))
                return false;

            return Environment.Is64BitProcess != targetWow64;
        }

        private static class Win32
        {
            public const int ProcessCreateThread = 0x0002;
            public const int ProcessQueryInformation = 0x0400;
            public const int ProcessVirtualMemoryOperation = 0x0008;
            public const int ProcessVirtualMemoryWrite = 0x0020;
            public const int ProcessVirtualMemoryRead = 0x0010;
            public const int MemCommit = 0x1000;
            public const int MemReserve = 0x2000;
            public const int MemRelease = 0x8000;
            public const int PageReadWrite = 0x04;
            public const int FileMapAllAccess = 0xF001F;

            [DllImport("kernel32.dll", SetLastError = true)] public static extern IntPtr OpenProcess(int access, bool inheritHandle, int processId);
            [DllImport("kernel32.dll", SetLastError = true)] public static extern bool CloseHandle(IntPtr handle);
            [DllImport("kernel32.dll", SetLastError = true)] public static extern IntPtr LoadLibrary(string fileName);
            [DllImport("kernel32.dll", SetLastError = true)] public static extern IntPtr GetModuleHandle(string moduleName);
            [DllImport("kernel32.dll", SetLastError = true)] public static extern IntPtr GetProcAddress(IntPtr module, string procName);
            [DllImport("kernel32.dll", SetLastError = true)] public static extern IntPtr VirtualAllocEx(IntPtr process, IntPtr address, UIntPtr size, int allocationType, int protect);
            [DllImport("kernel32.dll", SetLastError = true)] public static extern bool VirtualFreeEx(IntPtr process, IntPtr address, UIntPtr size, int freeType);
            [DllImport("kernel32.dll", SetLastError = true)] public static extern bool WriteProcessMemory(IntPtr process, IntPtr baseAddress, byte[] buffer, int size, out UIntPtr written);
            [DllImport("kernel32.dll", SetLastError = true)] public static extern IntPtr CreateRemoteThread(IntPtr process, IntPtr threadAttributes, uint stackSize, IntPtr startAddress, IntPtr parameter, uint creationFlags, IntPtr threadId);
            [DllImport("kernel32.dll", SetLastError = true)] public static extern uint WaitForSingleObject(IntPtr handle, uint milliseconds);
            [DllImport("kernel32.dll", SetLastError = true)] public static extern bool IsWow64Process(IntPtr process, out bool wow64Process);
            [DllImport("kernel32.dll", SetLastError = true)] public static extern IntPtr CreateFileMapping(IntPtr file, IntPtr attributes, int protect, int maximumSizeHigh, int maximumSizeLow, string name);
            [DllImport("kernel32.dll", SetLastError = true)] public static extern IntPtr MapViewOfFile(IntPtr mapping, int desiredAccess, int fileOffsetHigh, int fileOffsetLow, int bytesToMap);
        }
    }
}
