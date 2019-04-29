//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Fluent
{
    using System.Collections.Generic;

    internal sealed class PathsFluentDefinitionCore : PathsFluentDefinition
    {
        private readonly List<string> paths = new List<string>();
        private readonly IndexingPolicyFluentDefinitionCore parent;
        private readonly PathsFluentDefinitionType type;

        public PathsFluentDefinitionCore(
            IndexingPolicyFluentDefinitionCore parent,
            PathsFluentDefinitionType type)
        {
            this.parent = parent;
            this.type = type;
        }

        public override IndexingPolicyFluentDefinition Attach()
        {
            if (PathsFluentDefinitionType.Included.Equals(this.type))
            {
                this.parent.WithIncludedPaths(this.paths);
            }
            else
            {
                this.parent.WithExcludedPaths(this.paths);
            }

            return this.parent;
        }

        public override PathsFluentDefinition Path(string path)
        {
            this.paths.Add(path);
            return this;
        }
    }
}
