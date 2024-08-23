//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests.CFP.AllVersionsAndDeletes
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Used for CFP AllVersionsAndDeletes builder tests without having attribute annotations from STJ or NSJ.")]
    public class ToDoActivity
    {
        public string id { get; set; }

        public string pk { get; set; }

        public string description { get; set; }

        public int ttl { get; set; }
    }
}
