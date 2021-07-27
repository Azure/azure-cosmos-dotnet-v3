//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents.Rntbd
{
    using System;
    using System.Runtime.InteropServices;

    internal sealed class WindowsSystemUtilizationReader : SystemUtilizationReaderBase
    {
        private long lastIdleTime;
        private long lastKernelTime;
        private long lastUserTime;

        public WindowsSystemUtilizationReader()
        {
            this.lastIdleTime = 0;
            this.lastKernelTime = 0;
            this.lastUserTime = 0;
        }

        protected override float GetSystemWideCpuUsageCore()
        {
            long currentIdleTime;
            long currentKernelTime;
            long currentUserTime;

            if (!NativeMethods.GetSystemTimes(
                    out currentIdleTime, out currentKernelTime, out currentUserTime))
            {
                return Single.NaN;
            }

            long idleTimeElapsed = currentIdleTime - this.lastIdleTime;
            long kernelTimeElapsed = currentKernelTime - this.lastKernelTime;
            long userTimeElapsed = currentUserTime - this.lastUserTime;

            this.lastIdleTime = currentIdleTime;
            this.lastUserTime = currentUserTime;
            this.lastKernelTime = currentKernelTime;

            long timeElapsed = userTimeElapsed + kernelTimeElapsed;
            if (timeElapsed == 0)
            {
                return Single.NaN;
            }

            long busyTimeElapsed = userTimeElapsed + kernelTimeElapsed - idleTimeElapsed;

            return 100 * busyTimeElapsed / (float)timeElapsed;
        }

        protected override long GetSystemWideMemoryUsageCore()
        {
            NativeMethods.MemoryInfo memInfo = new NativeMethods.MemoryInfo();
            memInfo.dwLength = (uint)Marshal.SizeOf(memInfo);

            NativeMethods.GlobalMemoryStatusEx(out memInfo);

            return (long)(memInfo.ullTotalPhys - memInfo.ullAvailPhys);
        }

        private static class NativeMethods
        {

            /// <summary>
            /// Returns the sum of teh designated times across all processors since 
            /// system start in 100-nanosecond intervals
            /// https://docs.microsoft.com/en-us/windows/win32/api/processthreadsapi/nf-processthreadsapi-getsystemtimes
            /// </summary>
            /// <param name="idle">The amount of time that the system has been idle</param>
            /// <param name="kernel">
            /// The amount of time that the system has spent executing in Kernel mode 
            /// (including all threads in all processes, on all processors). This time value also includes 
            /// the amount of time the system has been idle.
            /// </param>
            /// <param name="user">
            /// The amount of time that the system has spent executing in User mode (including all threads 
            /// in all processes, on all processors).
            /// </param>
            /// <returns></returns>
            [DllImport("kernel32.dll", SetLastError = true)]
            internal static extern bool GetSystemTimes(out long idle, out long kernel, out long user);

            /// <summary>
            /// https://docs.microsoft.com/en-us/windows/win32/api/sysinfoapi/nf-sysinfoapi-globalmemorystatusex
            /// </summary>
            /// <param name="idle"></param>
            /// <param name="kernel"></param>
            /// <param name="user"></param>
            /// <returns></returns>
            [DllImport("kernel32.dll", SetLastError = true)]
            internal static extern bool GlobalMemoryStatusEx(out MemoryInfo memInfo);

            [StructLayout(LayoutKind.Sequential)]
            internal struct MemoryInfo
            {
                internal uint dwLength;
                internal uint dwMemoryLoad;
                internal ulong ullTotalPhys;
                internal ulong ullAvailPhys;
                internal ulong ullTotalPageFile;
                internal ulong ullAvailPageFile;
                internal ulong ullTotalVirtual;
                internal ulong ullAvailVirtual;
                internal ulong ullAvailExtendedVirtual;
            }
        }
    }
}