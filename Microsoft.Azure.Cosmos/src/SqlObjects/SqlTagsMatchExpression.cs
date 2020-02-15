//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Sql
{
    using System.Collections.Generic;

    internal class SqlTagsMatchExpression : SqlScalarExpression
    {
        private SqlTagsMatchExpression(string tagsProperty, IEnumerable<string> tags, bool supportDocumentRequiredTags)
            : base(SqlObjectKind.TagsMatch)
        {
            this.TagsProperty = tagsProperty;
            this.Tags = tags;
            this.SupportDocumentRequiredTags = supportDocumentRequiredTags;
        }

        public string TagsProperty { get; }
        public IEnumerable<string> Tags { get; }
        public bool SupportDocumentRequiredTags { get; }

        public static SqlTagsMatchExpression Create(string memberAccess, IEnumerable<string> tags, bool supportDocumentRequiredTags)
        {
            return new SqlTagsMatchExpression(memberAccess, tags, supportDocumentRequiredTags);
        }

        public override void Accept(SqlObjectVisitor visitor)
        {
            visitor.Visit(this);
        }

        public override TResult Accept<TResult>(SqlObjectVisitor<TResult> visitor)
        {
            return visitor.Visit(this);
        }

        public override TResult Accept<T, TResult>(SqlObjectVisitor<T, TResult> visitor, T input)
        {
            return visitor.Visit(this, input);
        }

        public override void Accept(SqlScalarExpressionVisitor visitor)
        {
            visitor.Visit(this);
        }

        public override TResult Accept<TResult>(SqlScalarExpressionVisitor<TResult> visitor)
        {
            return visitor.Visit(this);
        }

        public override TResult Accept<T, TResult>(SqlScalarExpressionVisitor<T, TResult> visitor, T input)
        {
            return visitor.Visit(this, input);
        }
    }
}