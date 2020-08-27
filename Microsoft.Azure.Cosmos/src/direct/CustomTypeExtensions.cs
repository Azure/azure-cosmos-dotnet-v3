//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Tracing;
    using System.Globalization;
    using System.IO;
    using System.Net.Sockets;
    using System.Reflection;
    using System.Runtime.InteropServices;
    using System.Security;
    using System.Security.Cryptography;
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
        public const int UnicodeEncodingCharSize = 2;

#if COSMOSCLIENT
        public const string SDKName = "cosmos-netstandard-sdk";
        public const string SDKVersion = "3.3.2";
#else
        public const string SDKName = "documentdb-netcore-sdk";
        public const string SDKVersion = "2.11.0";
#endif

        #region Type Extension Methods
#if (NETSTANDARD15 || NETSTANDARD16)
        public static bool IsAssignableFrom(this Type type, Type c)
        {
            return type.GetTypeInfo().IsAssignableFrom(c);
        }

        public static bool IsSubclassOf(this Type type, Type c)
        {
            return type.GetTypeInfo().IsSubclassOf(c);
        }

        public static MethodInfo GetMethod(this Type type, string name, BindingFlags bindingAttr)
        {
            return type.GetTypeInfo().GetMethod(name, bindingAttr);
        }

        public static MethodInfo GetMethod(this Type type, string name)
        {
            return type.GetTypeInfo().GetMethod(name);
        }
        public static MethodInfo GetMethod(this Type type, string name, Type[] types)
        {
            return type.GetTypeInfo().GetMethod(name, types);
        }

        public static Type[] GetGenericArguments(this Type type)
        {
            return type.GetTypeInfo().GetGenericArguments();
        }

        public static PropertyInfo GetProperty(this Type type, string name)
        {
            return type.GetTypeInfo().GetProperty(name);
        }

        public static PropertyInfo[] GetProperties(this Type type)
        {
            return type.GetTypeInfo().GetProperties();
        }

        public static PropertyInfo[] GetProperties(this Type type, BindingFlags bindingAttr)
        {
            return type.GetTypeInfo().GetProperties(bindingAttr);
        }

        public static Type[] GetInterfaces(this Type type)
        {
            return type.GetTypeInfo().GetInterfaces();
        }

        public static ConstructorInfo GetConstructor(this Type type, Type[] types)
        {
            return type.GetTypeInfo().GetConstructor(types);
        }

        public static T GetCustomAttribute<T>(this Type type, bool inherit) where T : Attribute
        {
            return type.GetTypeInfo().GetCustomAttribute<T>(inherit);
        }
#endif

#if NETSTANDARD16
        public static IEnumerable<Attribute> GetCustomAttributes(this Type type, Type attributeType, bool inherit)
        {
            return type.GetTypeInfo().GetCustomAttributes(attributeType, inherit);
        }
#endif

#endregion

        #region Misc Extension Methods
#if (NETSTANDARD15 || NETSTANDARD16)
        public static byte[] GetBuffer(this MemoryStream stream)
        {
            ArraySegment<byte> buffer;
            stream.TryGetBuffer(out buffer);
            return buffer.Array;
        }

        public static void Close(this Stream stream)
        {
            stream.Dispose();
        }

        public static void Close(this TcpClient tcpClient)
        {
            tcpClient.Dispose();
        }
#endif
#endregion

        #region Helper Methods
#if (NETSTANDARD15 || NETSTANDARD16)
        public static string GetLeftPart(this Uri uri, UriPartial part)
        {
            const UriComponents NonPathPart = (UriComponents.Scheme | UriComponents.UserInfo | UriComponents.Host | UriComponents.Port);

            switch (part)
            {
                case UriPartial.Authority:
                    return uri.GetComponents(NonPathPart, UriFormat.UriEscaped);

                case UriPartial.Path:
                    return uri.GetComponents(NonPathPart | UriComponents.Path, UriFormat.UriEscaped);

                case UriPartial.Query:
                    return uri.GetComponents(NonPathPart | UriComponents.Path | UriComponents.Query, UriFormat.UriEscaped);

                case UriPartial.Scheme:
                    return uri.GetComponents(UriComponents.Scheme | UriComponents.KeepDelimiter, UriFormat.UriEscaped);
            }

            throw new ArgumentException("Invalid part", nameof(part));
        }
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

        public static QueryRequestPerformanceActivity StartActivity(DocumentServiceRequest request)
        {
            return null;
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
            if(!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || IntPtr.Size != 8 || !ServiceInteropWrapper.AssembliesExist.Value)
            {
                DefaultTrace.TraceVerbose($"Bypass query parsing. IsWindowsOSPlatform {RuntimeInformation.IsOSPlatform(OSPlatform.Windows)} IntPtr.Size is {IntPtr.Size} ServiceInteropWrapper.AssembliesExist {ServiceInteropWrapper.AssembliesExist.Value}");
                return true;
            }

            return false;
        }

#endregion

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
