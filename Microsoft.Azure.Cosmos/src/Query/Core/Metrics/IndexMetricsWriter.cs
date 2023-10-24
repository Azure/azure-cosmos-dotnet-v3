//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Query.Core.Metrics
{
    using System;
    using System.Globalization;
    using System.Linq;
    using System.Text;

    /// <summary>
    /// Base class for visiting and serializing a <see cref="IndexUtilizationInfo"/>.
    /// </summary>
#if INTERNAL
#pragma warning disable SA1600
#pragma warning disable CS1591
    public
#else
    internal
#endif
    class IndexMetricsWriter
    {
        private const string IndexUtilizationInfo = "Index Utilization Information";
        private const string UtilizedSingleIndexes = "Utilized Single Indexes";
        private const string PotentialSingleIndexes = "Potential Single Indexes";
        private const string UtilizedCompositeIndexes = "Utilized Composite Indexes";
        private const string PotentialCompositeIndexes = "Potential Composite Indexes";
        private const string IndexExpression = "Index Spec";
        private const string IndexImpactScore = "Index Impact Score";

        private const string IndexUtilizationSeparator = "---";

        private readonly StringBuilder stringBuilder;

        public IndexMetricsWriter(StringBuilder stringBuilder)
        {
            this.stringBuilder = stringBuilder ?? throw new ArgumentNullException($"{nameof(stringBuilder)} must not be null.");
        }

        public void WriteIndexMetrics(IndexUtilizationInfo indexUtilizationInfo)
        {
            // IndexUtilizationInfo
            this.WriteBeforeIndexUtilizationInfo();

            this.WriteIndexUtilizationInfo(indexUtilizationInfo);

            this.WriteAfterIndexUtilizationInfo();
        }

        #region IndexUtilizationInfo
        protected void WriteBeforeIndexUtilizationInfo()
        {
            IndexMetricsWriter.AppendNewlineToStringBuilder(this.stringBuilder);
            IndexMetricsWriter.AppendHeaderToStringBuilder(
                this.stringBuilder,
                IndexMetricsWriter.IndexUtilizationInfo,
                indentLevel: 0);
        }

        protected void WriteIndexUtilizationInfo(IndexUtilizationInfo indexUtilizationInfo)
        {
            IndexMetricsWriter.AppendHeaderToStringBuilder(this.stringBuilder, IndexMetricsWriter.UtilizedSingleIndexes, indentLevel: 1);

            foreach (SingleIndexUtilizationEntity indexUtilizationEntity in indexUtilizationInfo.UtilizedSingleIndexes)
            {
                WriteSingleIndexUtilizationEntity(indexUtilizationEntity);
            }

            IndexMetricsWriter.AppendHeaderToStringBuilder(this.stringBuilder, IndexMetricsWriter.PotentialSingleIndexes, indentLevel: 1);

            foreach (SingleIndexUtilizationEntity indexUtilizationEntity in indexUtilizationInfo.PotentialSingleIndexes)
            {
                WriteSingleIndexUtilizationEntity(indexUtilizationEntity);
            }

            IndexMetricsWriter.AppendHeaderToStringBuilder(this.stringBuilder, IndexMetricsWriter.UtilizedCompositeIndexes, indentLevel: 1);

            foreach (CompositeIndexUtilizationEntity indexUtilizationEntity in indexUtilizationInfo.UtilizedCompositeIndexes)
            {
                WriteCompositeIndexUtilizationEntity(indexUtilizationEntity);
            }

            IndexMetricsWriter.AppendHeaderToStringBuilder(this.stringBuilder, IndexMetricsWriter.PotentialCompositeIndexes, indentLevel: 1);

            foreach (CompositeIndexUtilizationEntity indexUtilizationEntity in indexUtilizationInfo.PotentialCompositeIndexes)
            {
                WriteCompositeIndexUtilizationEntity(indexUtilizationEntity);
            }

            void WriteSingleIndexUtilizationEntity(SingleIndexUtilizationEntity indexUtilizationEntity)
            {
                IndexMetricsWriter.AppendHeaderToStringBuilder(this.stringBuilder, $"{IndexMetricsWriter.IndexExpression}: {indexUtilizationEntity.IndexDocumentExpression}", indentLevel: 2);
                IndexMetricsWriter.AppendHeaderToStringBuilder(this.stringBuilder, $"{IndexMetricsWriter.IndexImpactScore}: {indexUtilizationEntity.IndexImpactScore}", indentLevel: 2);
                IndexMetricsWriter.AppendHeaderToStringBuilder(this.stringBuilder, IndexMetricsWriter.IndexUtilizationSeparator, indentLevel: 2);
            }

            void WriteCompositeIndexUtilizationEntity(CompositeIndexUtilizationEntity indexUtilizationEntity)
            {
                IndexMetricsWriter.AppendHeaderToStringBuilder(this.stringBuilder, $"{IndexMetricsWriter.IndexExpression}: {String.Join(", ", indexUtilizationEntity.IndexDocumentExpressions)}", indentLevel: 2);
                IndexMetricsWriter.AppendHeaderToStringBuilder(this.stringBuilder, $"{IndexMetricsWriter.IndexImpactScore}: {indexUtilizationEntity.IndexImpactScore}", indentLevel: 2);
                IndexMetricsWriter.AppendHeaderToStringBuilder(this.stringBuilder, IndexMetricsWriter.IndexUtilizationSeparator, indentLevel: 2);
            }
        }

        protected void WriteAfterIndexUtilizationInfo()
        {
            // Do nothing
        }
        #endregion

        #region Helpers
        private static void AppendHeaderToStringBuilder(StringBuilder stringBuilder, string headerTitle, int indentLevel)
        {
            const string Indent = "  ";
            const string FormatString = "{0}{1}";

            stringBuilder.AppendFormat(
                CultureInfo.InvariantCulture,
                FormatString,
                string.Concat(Enumerable.Repeat(Indent, indentLevel)) + headerTitle,
                Environment.NewLine);
        }

        private static void AppendNewlineToStringBuilder(StringBuilder stringBuilder)
        {
            IndexMetricsWriter.AppendHeaderToStringBuilder(
                stringBuilder,
                string.Empty,
                indentLevel: 0);
        }
        #endregion
    }
}
