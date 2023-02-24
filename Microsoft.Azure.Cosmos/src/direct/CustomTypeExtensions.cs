//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.Tracing;
    using System.Globalization;
    using System.Net.Sockets;
    using System.Reflection;
    using System.Runtime.InteropServices;
    using System.Security;
    using System.Security.Cryptography;
    using System.Text;
    using Microsoft.Azure.Cosmos.Core.Trace;

    /// <summary>
    /// Extension class for defining methods/properties on Type class that are 
    /// not available on .NET Standard 1.6. This allows us to keep the same code
    /// we had earlier and when compiling for .NET Standard 1.6, we use these 
    /// extension methods that call GetTypeInfo() on the Type instance and call
    /// the corresponding method on it.
    /// 
    /// IsGenericType, IsEnum, IsValueType, IsInterface and BaseType are properties
    /// on Type class but since we cannot define "extension properties", I've converted 
    /// them to methods and return the underlying property value from the call to
    /// GetTypeInfo(). For .NET Framework, these extension methods simply return 
    /// the underlying property value.
    /// </summary>
    internal static class CustomTypeExtensions
    {
        public const int UnicodeEncodingCharSize = UnicodeEncoding.CharSize;
#if COSMOSCLIENT
        public const string SDKName = "cosmos-netstandard-sdk";
        public const string SDKVersion = "3.30.3";
        public const string SDKAssemblyName = "Microsoft.Azure.Cosmos.Client";
#else
        public const string SDKName = "documentdb-dotnet-sdk";
        public const string SDKVersion = "2.14.0";
        public const string SDKAssemblyName = "Microsoft.Azure.Documents.Client";
#endif

        public const string hostProcess32Bit = "32-bit";
        public const string hostProcess64Bit = "64-bit";

        // Example: Microsoft.Azure.Documents.Common/1.10.23.2
        private static readonly string InternalRequestBaseUserAgentString =
            Assembly.GetExecutingAssembly().GetName().Name + "/" +
            Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyFileVersionAttribute>().Version;

        public static bool IsGenericType(this Type type)
        {
            return type.IsGenericType;
        }

        public static bool IsEnum(this Type type)
        {
            return type.IsEnum;
        }

        public static bool IsValueType(this Type type)
        {
            return type.IsValueType;
        }

        public static bool IsInterface(this Type type)
        {
            return type.IsInterface;
        }

        public static Type GetBaseType(this Type type)
        {
            return type.BaseType;
        }

        public static Type GeUnderlyingSystemType(this Type type)
        {
            return type.UnderlyingSystemType;
        }

        public static Assembly GetAssembly(this Type type)
        {
            return type.Assembly;
        }

        public static ConstructorInfo GetConstructor(this Type type, Type[] types)
        {
            return type.GetConstructor(types);
        }

        public static IEnumerable<CustomAttributeData> GetsCustomAttributes(this MemberInfo memberInfo)
        {
            return memberInfo.CustomAttributes;
        }

        public static Delegate CreateDelegate(Type delegateType, object target, MethodInfo methodInfo)
        {
            return Delegate.CreateDelegate(delegateType, target, methodInfo);
        }

        public static IntPtr SecureStringToCoTaskMemAnsi(SecureString secureString)
        {
            return System.Runtime.InteropServices.Marshal.SecureStringToCoTaskMemAnsi(secureString);
        }

        public static void SetActivityId(ref Guid id)
        {
            Trace.CorrelationManager.ActivityId = id;
#if NETFX45
            System.Diagnostics.Eventing.EventProvider.SetActivityId(ref id);
#else
            EventSource.SetCurrentThreadActivityId(id);
#endif
        }

        public static Random GetRandomNumber()
        {
            using (RNGCryptoServiceProvider cryptoProvider = new RNGCryptoServiceProvider())
            {
                byte[] seedArray = new byte[sizeof (int)];
                cryptoProvider.GetBytes(seedArray);
                return new Random(BitConverter.ToInt32(seedArray, 0));
            }
        }

        public static QueryRequestPerformanceActivity StartActivity(DocumentServiceRequest request)
        {
            QueryRequestPerformanceActivity activity = null;

            bool isQuery = !string.IsNullOrEmpty(request.Headers[HttpConstants.HttpHeaders.Query]);
            if (isQuery || request.OperationType == OperationType.Query)
            {
                activity = new QueryRequestPerformanceActivity();
                activity.ActivityStart();
            }

            return activity;
        }

        public static string GenerateBaseUserAgentString()
        {
            // For all requests that are initiated by Client SDK, we want a more meaningful user agent, to be used for telemetry purposes
            if (string.CompareOrdinal(Assembly.GetExecutingAssembly().GetName().Name, SDKAssemblyName) == 0)
            {
                string osVersion = Environment.OSVersion.VersionString; // Microsoft Windows NT 6.2.9200.0
                string trimmedOsVersion = osVersion.Replace(" ", string.Empty); // MicrosoftWindowsNT6.2.9200.0
                // This format is required for user agents in HTTP requests(no space, name and version separated with /)
                string formattedOsVersion = trimmedOsVersion.Replace(Environment.OSVersion.Version.ToString(),
                    "/" + Environment.OSVersion.Version); // MicrosoftWindowsNT/6.2.9200.0

                string hostProcess = ServiceInteropWrapper.Is64BitProcess ? hostProcess64Bit : hostProcess32Bit;

                // Example: documentdb-dotnet-sdk/1.10.0 Host/64-bit MicrosoftWindowsNT/6.2.9200.0
                return string.Format(CultureInfo.InvariantCulture, "{0}/{1} Host/{2} {3}",
                    SDKName,
                    SDKVersion,
                    hostProcess,
                    formattedOsVersion);
            }
            // For all other internal requests, we will continue using the earlier format
            else
            {
                // Example: Microsoft.Azure.Documents.Common/1.10.23.2
                return CustomTypeExtensions.InternalRequestBaseUserAgentString;
            }
        }

        public static bool ConfirmOpen(Socket socket)
        {
            NativeMethods.WSAPOLLFD[] poll = new NativeMethods.WSAPOLLFD[1];

            poll[0].fd = socket.Handle;
            poll[0].events = NativeMethods.POLLWRNORM;
            poll[0].revents = 0;

            GCHandle handlePoll = GCHandle.Alloc(poll, GCHandleType.Pinned);
            try
            {
                Int32 result = NativeMethods.WSAPoll(handlePoll.AddrOfPinnedObject(), 1, 0);
                if (NativeMethods.SOCKET_ERROR == result)
                {
                    return false;
                }
                else if ((poll[0].revents & (NativeMethods.POLLERR | NativeMethods.POLLHUP | NativeMethods.POLLNVAL)) !=
                         0)
                {
                    return false;
                }
                else
                {
                    return true;
                }
            }
            finally
            {
                handlePoll.Free();
            }
        }

        // By pass query parsing on 32 bit processes or if interop assemblies don't exist
        public static bool ByPassQueryParsing()
        {
            if(!ServiceInteropWrapper.Is64BitProcess || !ServiceInteropWrapper.AssembliesExist.Value){
                DefaultTrace.TraceVerbose($"Bypass query parsing. IntPtr.Size is {IntPtr.Size} ServiceInteropWrapper.AssembliesExist {ServiceInteropWrapper.AssembliesExist.Value}");
                return true;
            }

            return false;
        }

        private static class NativeMethods
        {
            public const Int32 SOCKET_ERROR = -1;

            public const Int16 POLLRDNORM = 0x0100;
            public const Int16 POLLRDBAND = 0x0200;
            public const Int16 POLLIN = POLLRDNORM | POLLRDBAND;
            public const Int16 POLLPRI = 0x0400;

            public const Int16 POLLWRNORM = 0x0010;
            public const Int16 POLLOUT = POLLWRNORM;
            public const Int16 POLLWRBAND = 0x0020;

            public const Int16 POLLERR = 0x0001;
            public const Int16 POLLHUP = 0x0002;
            public const Int16 POLLNVAL = 0x0004;

            [StructLayout(LayoutKind.Sequential)]
            public struct WSAPOLLFD
            {
                public IntPtr fd;
                public Int16 events;
                public Int16 revents;
            }

            [DllImport("ws2_32.dll", SetLastError = true)]
            public static extern Int32 WSAPoll(
                IntPtr fds,
                UInt32 fdCount,
                Int32 timeout);
        }
    }
}
