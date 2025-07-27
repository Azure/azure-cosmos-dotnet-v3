//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Tracing;
    using System.Globalization;
    using System.Net.Sockets;
    using System.Reflection;
    using System.Runtime.InteropServices;
    using System.Security;
    using System.Security.Cryptography;
    using Microsoft.Azure.Cosmos.Core.Trace;

    /// <summary>
    /// Extension class for .NET 9 Azure Cosmos DB SDK.
    /// Provides utility methods for the Cosmos DB client library.
    /// </summary>
    internal static class CustomTypeExtensions
    {
        public const int UnicodeEncodingCharSize = 2;

#if COSMOSCLIENT
        public const string SDKName = "cosmos-netstandard-sdk";
        public const string SDKVersion = "3.18.0";
#else
        public const string SDKName = "documentdb-netcore-sdk";
        public const string SDKVersion = "2.14.0";
#endif

        public static Delegate CreateDelegate(Type delegateType, object target, MethodInfo methodInfo)
        {
            return methodInfo.CreateDelegate(delegateType, target);
        }

        public static IntPtr SecureStringToCoTaskMemAnsi(SecureString secureString)
        {
            return SecureStringMarshal.SecureStringToCoTaskMemAnsi(secureString);
        }

        public static void SetActivityId(ref Guid id)
        {
            EventSource.SetCurrentThreadActivityId(id);
        }

        public static Random GetRandomNumber()
        {
            using (RandomNumberGenerator randomNumberGenerator = RandomNumberGenerator.Create())
            {
                byte[] seedArray = new byte[sizeof(int)];
                randomNumberGenerator.GetBytes(seedArray);
                return new Random(BitConverter.ToInt32(seedArray, 0));
            }
        }

        public static string GenerateBaseUserAgentString()
        {
            string version = PlatformApis.GetOSVersion();

            // Example: Windows/10.0.14393 documentdb-netcore-sdk/0.0.1
            return string.Format(CultureInfo.InvariantCulture, "{0}/{1} {2}/{3}",
            PlatformApis.GetOSPlatform(),
            String.IsNullOrEmpty(version) ? "Unknown" : version.Trim(),
            SDKName,
            SDKVersion);
        }

        // This is how you can determine whether a socket is still connected.
        public static bool ConfirmOpen(Socket socket)
        {   
            bool blockingState = socket.Blocking;

            try
            {
                byte[] tmp = new byte[1];

                // Make a nonblocking, zero-byte Send call
                socket.Blocking = false;
                socket.Send(tmp, 0, 0);
                return true;
            }

            catch (SocketException ex)
            {
                // If the Send call throws a WAEWOULDBLOCK error code (10035), then the socket is still connected; otherwise, the socket is no longer connected
                return (ex.SocketErrorCode == SocketError.WouldBlock);
            }

            catch (ObjectDisposedException)
            {
                // Send with throw ObjectDisposedException if the Socket has been closed
                return false;
            }

            finally
            {
                socket.Blocking = blockingState;
            }
        }

        // Bypass query parsing on 32 bit process on Windows and always on non-Windows(Linux/OSX) platforms or if interop assemblies don't exist.
        public static bool ByPassQueryParsing()
        {            
            if(!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || !ServiceInteropWrapper.Is64BitProcess || !ServiceInteropWrapper.AssembliesExist.Value)
            {
                DefaultTrace.TraceVerbose($"Bypass query parsing. IsWindowsOSPlatform {RuntimeInformation.IsOSPlatform(OSPlatform.Windows)} IntPtr.Size is {IntPtr.Size} ServiceInteropWrapper.AssembliesExist {ServiceInteropWrapper.AssembliesExist.Value}");
                return true;
            }

            return false;
        }

        #region Properties converted to Methods
        public static bool IsGenericType(this Type type)
        {
            return type.GetTypeInfo().IsGenericType;
        }

        public static bool IsEnum(this Type type)
        {
            return type.GetTypeInfo().IsEnum;
        }

        public static bool IsValueType(this Type type)
        {
            return type.GetTypeInfo().IsValueType;
        }

        public static bool IsInterface(this Type type)
        {
            return type.GetTypeInfo().IsInterface;
        }

        public static Type GetBaseType(this Type type)
        {
            return type.GetTypeInfo().BaseType;
        }

        public static Type GeUnderlyingSystemType(this Type type)
        {
            return type.GetTypeInfo().UnderlyingSystemType;
        }

        public static Assembly GetAssembly(this Type type)
        {
            return type.GetTypeInfo().Assembly;
        }

        public static IEnumerable<CustomAttributeData> GetsCustomAttributes(this Type type)
        {
            return type.GetTypeInfo().CustomAttributes;
        }
        #endregion
    }
}
