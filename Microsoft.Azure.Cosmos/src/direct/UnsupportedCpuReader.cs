//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents.Rntbd
{
    using System;

    internal sealed class UnsupportedCpuReader : CpuReaderBase
    {
        public UnsupportedCpuReader() : base()
        {
        }

        protected override float GetSystemWideCpuUsageCore()
        {
            return Single.NaN;
        }
    }
}