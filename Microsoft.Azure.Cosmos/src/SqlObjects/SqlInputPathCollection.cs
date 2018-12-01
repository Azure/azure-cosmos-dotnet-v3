//-----------------------------------------------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//-----------------------------------------------------------------------------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Sql
{
    using System;
    using System.Text;

    internal sealed class SqlInputPathCollection : SqlCollection
    {
        public SqlIdentifier Input
        {
            get;
            private set;
        }

        public SqlPathExpression RelativePath
        {
            get;
            private set;
        }

        public SqlInputPathCollection(
            SqlIdentifier input,
            SqlPathExpression relativePath)
            : base(SqlObjectKind.InputPathCollection)
        {
            if (input == null)
            {
                throw new ArgumentNullException("input");
            }
            
            this.Input = input;
            this.RelativePath = relativePath;
        }

        public override void AppendToBuilder(StringBuilder builder)
        {
            this.Input.AppendToBuilder(builder);
            if (this.RelativePath != null)
            {
                this.RelativePath.AppendToBuilder(builder);
            }
        }
    }
}
