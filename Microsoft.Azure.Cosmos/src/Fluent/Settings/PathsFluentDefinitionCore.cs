//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Fluent
{
    using System;
    using System.Collections.Generic;

    internal sealed class PathsFluentDefinitionCore : PathsFluentDefinition
    {
        private readonly List<string> paths = new List<string>();
        private readonly IndexingPolicyFluentDefinition parent;
        private readonly Action<IEnumerable<string>> attachCallback;

        public PathsFluentDefinitionCore(
            IndexingPolicyFluentDefinition parent,
            Action<IEnumerable<string>> attachCallback)
        {
            this.parent = parent;
            this.attachCallback = attachCallback;
        }

        public override IndexingPolicyFluentDefinition Attach()
        {
            this.attachCallback(this.paths);
            return this.parent;
        }

        public override PathsFluentDefinition Path(string path)
        {
            this.paths.Add(path);
            return this;
        }
    }
}
