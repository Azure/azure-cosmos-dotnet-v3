// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Pagination
{
    using System;
    using System.Collections.Generic;

    internal sealed class DocumentContainerPage : Page<DocumentContainerState>
    {
        public DocumentContainerPage(
            IReadOnlyList<Record> records,
            DocumentContainerState state = null)
            : base(state)
        {
            this.Records = records ?? throw new ArgumentNullException(nameof(records));
        }

        public IReadOnlyList<Record> Records { get; }
    }
}
