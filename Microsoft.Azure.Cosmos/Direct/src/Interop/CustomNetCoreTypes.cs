//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents
{
    using System;
    using System.Net.Security;
    using System.Runtime.InteropServices;

#if NETSTANDARD15 || NETSTANDARD16

    /// <summary>
    /// This is a hack to make MessageContractMemberAttribute mean nothing when compiling for .NET Standard 1.6
    /// so as to avoid adding #if/#endif around it in the StoreResponse class that uses them.
    /// </summary>
    internal abstract partial class MessageContractMemberAttribute : Attribute
    {
        public bool HasProtectionLevel { get { return default(bool); } }

        public string Name { get { return default(string); } set { } }

        public string Namespace { get { return default(string); } set { } }

        public ProtectionLevel ProtectionLevel { get { return default(ProtectionLevel); } set { } }
    }

    /// <summary>
    /// This is a hack to make MessageContractAttribute mean nothing when compiling for .NET Standard 1.6
    /// so as to avoid adding #if/#endif around it in the StoreResponse class that uses them.
    /// </summary>
    internal sealed partial class MessageContractAttribute : Attribute
    {
        public bool HasProtectionLevel { get { return default(bool); } }

        public bool IsWrapped { get { return default(bool); } set { } }

        public ProtectionLevel ProtectionLevel { get { return default(ProtectionLevel); } set { } }

        public string WrapperName { get { return default(string); } set { } }

        public string WrapperNamespace { get { return default(string); } set { } }
    }

    /// <summary>
    /// This is a hack to make MessageBodyMemberAttribute mean nothing when compiling for .NET Standard 1.6
    /// so as to avoid adding #if/#endif around it in the StoreResponse class that uses them.
    /// </summary>
    internal class MessageBodyMemberAttribute : MessageContractMemberAttribute
    {
        public int Order { get { return default(int); } set { } }
    }

    /// <summary>
    /// This is a hack to make MessageHeaderAttribute mean nothing when compiling for .NET Standard 1.6
    /// so as to avoid adding #if/#endif around it in the StoreResponse class that uses them.
    /// </summary>
    internal class MessageHeaderAttribute : MessageContractMemberAttribute
    {
        public string Actor { get { return default(string); } set { } }

        public bool MustUnderstand { get { return default(bool); } set { } }

        public bool Relay { get { return default(bool); } set { } }
    }

    /// <summary>
    /// This is a hack to add ICloneable interface as part of our namespace since it doesn't exist in .NET Standard 1.6
    /// That way we will avoid adding #if/#endif around the classes that currently implement it. Any .NET Core 1.0 app
    /// will not be using this type anyways.
    /// </summary>
    [ComVisible(true)]
    internal interface ICloneable
    {
        Object Clone();
    }

    /// <summary>
    /// This is a hack to make Serializable attribute mean nothing when compiling for .NET Standard 1.6
    /// so as to avoid adding #if/#endif around it in the entire codebase.
    /// </summary>
    internal class SerializableAttribute : Attribute
    {
        
    }

    /// <summary>
    /// This replaces the use of ConfigurationErrorsException with Exception. This exception is thrown only
    /// in one internal method and is not caught within our code, so it'ss safe to use Exception here.
    /// </summary>
    internal class ConfigurationErrorsException : Exception
    {
        public ConfigurationErrorsException(string message)
            :base(message)
        {
            
        }
    }

    /// <summary>
    /// CorrelationManager is not yet available in .NET Standard 1.6 and will be available in .NET Standard 2.0
    /// Got the source code from corefx repo and exposing it here from the Trace class.
    /// </summary>
    internal sealed class Trace
    {
        private static CorrelationManager s_correlationManager = null;

        public static CorrelationManager CorrelationManager
        {
            get
            {
                if (s_correlationManager == null)
                {
                    s_correlationManager = new CorrelationManager();
                }

                return s_correlationManager;
            }
        }
    }

    /// <summary>
    /// Used in UriUtility class. Not available in .NET Standard 1.6
    /// </summary>
    internal enum UriPartial
    {
        Scheme,
        Authority,
        Path,
        Query
    }
#endif

    /// <summary>
    /// Dummy QueryRequestPerformanceActivity class as we don't support PerfCounters yet.
    /// </summary>
    internal class QueryRequestPerformanceActivity
    {
        public void ActivityComplete(bool markComplete)
        {

        }
    }
}
