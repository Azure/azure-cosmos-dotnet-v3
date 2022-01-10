//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents.Rntbd
{
    using System;

    internal sealed class UnsupportedSystemUtilizationReader : SystemUtilizationReaderBase
    {
        public UnsupportedSystemUtilizationReader() : base()
        {
        }

        protected override float GetSystemWideCpuUsageCore()
        {
            return Single.NaN;
        }

        protected override long? GetSystemWideMemoryAvailabiltyCore()
        {
            return null;
        }
    }
}