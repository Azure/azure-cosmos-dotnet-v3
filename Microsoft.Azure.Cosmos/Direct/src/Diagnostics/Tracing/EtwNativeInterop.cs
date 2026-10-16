// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Core.Trace
{
    using System;
    using System.Runtime.InteropServices;
    using Microsoft.Win32.SafeHandles;

    /// <summary>
    /// Native ETW-related methods from advapi32.dll.
    /// </summary>
    internal static class EtwNativeInterop
    {
        [DllImport("advapi32.dll", ExactSpelling = true)]
        internal static extern uint EventRegister(
            in Guid providerId,
            IntPtr enableCallback,
            IntPtr callbackContext,
            ref ProviderHandle registrationHandle);

        [DllImport("advapi32.dll", ExactSpelling = true)]
        internal static extern uint EventUnregister(IntPtr registrationHandle);

        [DllImport("advapi32.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
        public static extern uint EventWriteString(
            ProviderHandle registrationHandle,
            byte level,
            long keywords,
            string message);

        /// <inheritdoc />
        internal class ProviderHandle : SafeHandleZeroOrMinusOneIsInvalid
        {
            public ProviderHandle()
                : base(true)
            {
            }

            protected override bool ReleaseHandle() => EtwNativeInterop.EventUnregister(this.handle) == 0;
        }
    }
}