//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Reflection;
    using System.Runtime.InteropServices;
    using Microsoft.Azure.Cosmos.Core.Trace;

    internal static class ServiceInteropWrapper
    {
        internal static Lazy<bool> AssembliesExist = new Lazy<bool>(() =>
        {
            return ServiceInteropWrapper.CheckIfAssembliesExist(out string _);
        });

        static ServiceInteropWrapper()
        {
            ServiceInteropWrapper.Is64BitProcess = IntPtr.Size == 8;

#if NETFX
            // Framework only works on Windows
            ServiceInteropWrapper.IsWindowsOSPlatform = true;
#else
            ServiceInteropWrapper.IsWindowsOSPlatform = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
#endif
        }

        internal static readonly bool Is64BitProcess;

        internal static readonly bool IsWindowsOSPlatform;

        // ServiceInterop is client level option based on ConnectionPolicy.QueryPlanGenerationMode
        internal static bool UseServiceInterop(QueryPlanGenerationMode queryPlanRetrievalMode)
        {
            switch (queryPlanRetrievalMode)
            {
                case QueryPlanGenerationMode.GatewayOnly:
                    return false;
                case QueryPlanGenerationMode.WindowsX64NativeOnly:
                    return true;
                case QueryPlanGenerationMode.DefaultWindowsX64NativeWithFallbackToGateway:
                    return !CustomTypeExtensions.ByPassQueryParsing();
                default:
                    Debug.Fail($"Unexpected {nameof(QueryPlanGenerationMode)}: {queryPlanRetrievalMode}");
                    return !CustomTypeExtensions.ByPassQueryParsing();
            }
        }

        /// <summary>
        /// Use AssembliesExist for all code paths. 
        /// This function is used in testing to validate different overrides.
        /// </summary>
        internal static bool CheckIfAssembliesExist(out string validationMessage)
        {
            validationMessage = string.Empty;
            try
            {
                if (!ServiceInteropWrapper.IsGatewayAllowedToParseQueries())
                {
                    // Gateway is not allowed, skip validation and let runtime fail in-case of interop DLL non-existence
                    validationMessage = $"The environment variable {ServiceInteropWrapper.AllowGatewayToParseQueries} is overriding the service interop if exists validation.";
                    return true;
                }

#if !NETSTANDARD16
                DefaultTrace.TraceInformation($"Assembly location: {Assembly.GetExecutingAssembly().Location}");
                if (Assembly.GetExecutingAssembly().IsDynamic)
                {
                    validationMessage = $"The service interop if exists validation skipped because Assembly.GetExecutingAssembly().IsDynamic is true";
                    return true;
                }

                string binDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
#else
                DefaultTrace.TraceInformation($"Assembly location: {(typeof(ServiceInteropWrapper).GetTypeInfo().Assembly.Location)}");
                if (typeof(ServiceInteropWrapper).GetTypeInfo().Assembly.IsDynamic)
                {
                    validationMessage = $"The service interop if exists validation skipped because typeof(ServiceInteropWrapper).GetTypeInfo().Assembly.IsDynamic is true";
                    return true;
                }
            
                // For NetCore check the entry assembly's path first (if available) since the interop DLL is copied to the application output directory
                // (as specified in the Nuget package's target)
                Assembly assembly = System.Reflection.Assembly.GetEntryAssembly() ?? typeof(ServiceInteropWrapper).GetTypeInfo().Assembly;
                string binDir = Path.GetDirectoryName(assembly.Location);
#endif

                string dll = 
#if COSMOSCLIENT
                "Microsoft.Azure.Cosmos.ServiceInterop.dll";
#else
                "Microsoft.Azure.Documents.ServiceInterop.dll";
#endif

                string dllPath = Path.Combine(binDir, dll);
                validationMessage = $"The service interop location checked at {dllPath}";

                if (!File.Exists(dllPath))
                {
                    DefaultTrace.TraceInformation($"ServiceInteropWrapper assembly not found at {dllPath}");
                    return false;
                }

                return true;
            }
            catch (Exception e)
            {
                // There has been certain environments where attempting to find the ServiceInterop has resulted in an exception.
                // Instead of failing the SDK trace the exception and fall back to gateway mode.
                DefaultTrace.TraceWarning($"ServiceInteropWrapper: Falling back to gateway. Finding ServiceInterop dll threw an exception {e}");
            }

            if (string.IsNullOrEmpty(validationMessage))
            {
                validationMessage = $"An unexpected exception occurred while checking the file location";
            }
            
            return false;
        }

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
                [MarshalAs(UnmanagedType.LPWStr)][In] string query,
                [In] bool requireFormattableOrderByQuery,
                [In] bool isContinuationExpected,
                [In] bool allowNonValueAggregateQuery,
                [In] bool hasLogicalPartitionKey,
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
                [MarshalAs(UnmanagedType.LPStr)][In] string configJsonString,
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
                [MarshalAs(UnmanagedType.LPStr)][In] string configJsonString);

        private const string DisableSkipInterop = "DisableSkipInterop"; // Used by V2 SDK Only
        private const string AllowGatewayToParseQueries = "AllowGatewayToParseQueries"; // Used by V3 SDK Only
        internal static bool IsGatewayAllowedToParseQueries()
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
                if (Boolean.TryParse(boolValueString, out parsedBoolValue))
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
