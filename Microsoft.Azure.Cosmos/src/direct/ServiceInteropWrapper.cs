//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Reflection;
    using System.Runtime.InteropServices;
    using System.Text;
    using Microsoft.Azure.Cosmos.Core.Trace;

    internal static class ServiceInteropWrapper
    {
        internal static Lazy<bool> AssembliesExist = new Lazy<bool>(() => {
            if (!ServiceInteropWrapper.IsGatewayAllowedToParseQueries())
            {
                // Gateway is not allowed, skip valiation and let runtime fail in-case of interop DLL non-existence
                return true;
            }

#if !NETSTANDARD16
            DefaultTrace.TraceInformation($"Assembly location: {Assembly.GetExecutingAssembly().Location}");
            if (Assembly.GetExecutingAssembly().IsDynamic)
            {
                return true;
            }
            string binDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
#else
            DefaultTrace.TraceInformation($"Assembly location: {(typeof(ServiceInteropWrapper).GetTypeInfo().Assembly.Location)}");
            if (typeof(ServiceInteropWrapper).GetTypeInfo().Assembly.IsDynamic)
            {
                return true;
            }
            
            // For NetCore check the entry assembly's path first (if available) since the interop DLL is copied to the application output directory
            // (as specified in the Nuget package's target)
            Assembly assembly = System.Reflection.Assembly.GetEntryAssembly() ?? typeof(ServiceInteropWrapper).GetTypeInfo().Assembly;
            string binDir = Path.GetDirectoryName(assembly.Location);
#endif
            string[] nativeDll = new string[]{
#if COSMOSCLIENT
                "Microsoft.Azure.Cosmos.ServiceInterop.dll"
#else
                "Microsoft.Azure.Documents.ServiceInterop.dll"
#endif
            };

            foreach (string dll in nativeDll)
            {
                string dllPath = Path.Combine(binDir, dll);
                if (!File.Exists(dllPath))
                {
                    DefaultTrace.TraceVerbose($"ServiceInteropWrapper assembly not found at {dllPath}");
                    return false;
                }
            }
            return true;
        });

#if !NETSTANDARD16
        [System.Security.SuppressUnmanagedCodeSecurity]
#endif
#if COSMOSCLIENT
        [DllImport("Microsoft.Azure.Cosmos.ServiceInterop.dll", SetLastError = true, CallingConvention = CallingConvention.Cdecl, BestFitMapping = false, ThrowOnUnmappableChar = true)]
#else
        [DllImport("Microsoft.Azure.Documents.ServiceInterop.dll", SetLastError = true, CallingConvention = CallingConvention.Cdecl, BestFitMapping = false, ThrowOnUnmappableChar = true)]
#endif
        public static extern
        uint GetPartitionKeyRangesFromQuery(
                [In] IntPtr serviceProvider,
                [MarshalAs(UnmanagedType.LPWStr)] [In] string query,
                [In] bool requireFormattableOrderByQuery,
                [In] bool isContinuationExpected,
                [In] bool allowNonValueAggregateQuery,
                [In] bool hasLogicalPartitionKey,
                [MarshalAs(UnmanagedType.LPArray, ArraySubType=UnmanagedType.LPWStr)] [In] string[] partitionKeyDefinitionPathTokens,
                [MarshalAs(UnmanagedType.LPArray)] [In] uint[] partitionKeyDefinitionPathTokenLengths,
                [In] uint partitionKeyDefinitionPathCount,
                [In] PartitionKind partitionKind,
                [In, Out] IntPtr serializedQueryExecutionInfoBuffer,
                [In] uint serializedQueryExecutionInfoBufferLength,
                [Out] out uint serializedQueryExecutionInfoResultLength);

#if !NETSTANDARD16
        [System.Security.SuppressUnmanagedCodeSecurity]
#endif
#if COSMOSCLIENT
        [DllImport("Microsoft.Azure.Cosmos.ServiceInterop.dll", SetLastError = true, CallingConvention = CallingConvention.Cdecl, BestFitMapping = false, ThrowOnUnmappableChar = true)]
#else
        [DllImport("Microsoft.Azure.Documents.ServiceInterop.dll", SetLastError = true, CallingConvention = CallingConvention.Cdecl, BestFitMapping = false, ThrowOnUnmappableChar = true)]
#endif
        public static extern
        uint GetPartitionKeyRangesFromQuery2(
                [In] IntPtr serviceProvider,
                [MarshalAs(UnmanagedType.LPWStr)][In] string query,
                [In] bool requireFormattableOrderByQuery,
                [In] bool isContinuationExpected,
                [In] bool allowNonValueAggregateQuery,
                [In] bool hasLogicalPartitionKey,
                [In] bool bAllowDCount,
                [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.LPWStr)][In] string[] partitionKeyDefinitionPathTokens,
                [MarshalAs(UnmanagedType.LPArray)][In] uint[] partitionKeyDefinitionPathTokenLengths,
                [In] uint partitionKeyDefinitionPathCount,
                [In] PartitionKind partitionKind,
                [In, Out] IntPtr serializedQueryExecutionInfoBuffer,
                [In] uint serializedQueryExecutionInfoBufferLength,
                [Out] out uint serializedQueryExecutionInfoResultLength);

#if !NETSTANDARD16
        [System.Security.SuppressUnmanagedCodeSecurity]
#endif
#if COSMOSCLIENT
        [DllImport("Microsoft.Azure.Cosmos.ServiceInterop.dll", SetLastError = true, CallingConvention = CallingConvention.Cdecl, BestFitMapping = false, ThrowOnUnmappableChar = true)]
#else
        [DllImport("Microsoft.Azure.Documents.ServiceInterop.dll", SetLastError = true, CallingConvention = CallingConvention.Cdecl, BestFitMapping = false, ThrowOnUnmappableChar = true)]
#endif
        public static extern
        uint CreateServiceProvider(
                [MarshalAs(UnmanagedType.LPStr)] [In] string configJsonString,
                [Out] out IntPtr serviceProvider);

#if !NETSTANDARD16
        [System.Security.SuppressUnmanagedCodeSecurity]
#endif
#if COSMOSCLIENT
        [DllImport("Microsoft.Azure.Cosmos.ServiceInterop.dll", SetLastError = true, CallingConvention = CallingConvention.Cdecl, BestFitMapping = false, ThrowOnUnmappableChar = true)]
#else
        [DllImport("Microsoft.Azure.Documents.ServiceInterop.dll", SetLastError = true, CallingConvention = CallingConvention.Cdecl, BestFitMapping = false, ThrowOnUnmappableChar = true)]
#endif
        public static extern
        uint UpdateServiceProvider(
                [In] IntPtr serviceProvider,
                [MarshalAs(UnmanagedType.LPStr)] [In] string configJsonString);

        private const string DisableSkipInterop = "DisableSkipInterop"; // Used by V2 SDK Only
        private const string AllowGatewayToParseQueries = "AllowGatewayToParseQueries"; // Used by V3 SDK Only
        internal  static bool IsGatewayAllowedToParseQueries()
        {
            bool? allowGatewayToParseQueries = ServiceInteropWrapper.GetSetting(ServiceInteropWrapper.AllowGatewayToParseQueries);

            if (allowGatewayToParseQueries != null)
            {
                return allowGatewayToParseQueries.Value;
            }
#if !COSMOSCLIENT
            // V2 SDK client uses below logic
            bool? disableSkipInteropConfig = ServiceInteropWrapper.GetSetting(ServiceInteropWrapper.DisableSkipInterop);

            if (disableSkipInteropConfig != null)
            {
                return !disableSkipInteropConfig.Value;
            }
#endif
            // Default allow skip gateway
            return true;
        }

        // null return is either invalid setting or non-deterministric
        private static bool? BoolParse(string boolValueString)
        {
            if (!string.IsNullOrEmpty(boolValueString))
            {
                // Net standard 2.0 & net461 target breaks Boolean.TryParse!!
                // Fall back to case in-sensitive string comparison 
                if (string.Equals(Boolean.TrueString, boolValueString, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(1.ToString(), boolValueString, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                if (string.Equals(Boolean.FalseString, boolValueString, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(0.ToString(), boolValueString, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                bool parsedBoolValue = false;
                if (!Boolean.TryParse(boolValueString, out parsedBoolValue))
                {
                    return parsedBoolValue;
                }
            }

            return null;
        }

        private static bool? GetSetting(string key)
        {
            string env = Environment.GetEnvironmentVariable(key);
            DefaultTrace.TraceInformation($"ServiceInteropWrapper read {key} environment variable as {env}");
            bool? value = ServiceInteropWrapper.BoolParse(env);
            DefaultTrace.TraceInformation($"ServiceInteropWrapper read  parsed {key} environment variable as {value}");

            if (value.HasValue)
            {
                return value.Value;
            }

#if !(NETSTANDARD15 || NETSTANDARD16)
            string setting = System.Configuration.ConfigurationManager.AppSettings[key];
            DefaultTrace.TraceInformation($"ServiceInteropWrapper read {key} from AppConfig as {setting} ");
            value = ServiceInteropWrapper.BoolParse(setting);
            DefaultTrace.TraceInformation($"ServiceInteropWrapper read parsed {key} AppConfig as {value} ");
#endif

            return value;
        }
    }
}
