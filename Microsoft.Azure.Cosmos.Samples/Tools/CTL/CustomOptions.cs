//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace CosmosCTL
{
    using Microsoft.Extensions.Logging.Console;

    public sealed class CustomOptions : ConsoleFormatterOptions
    {
        public string? CustomPrefix { get; set; }
    }
}