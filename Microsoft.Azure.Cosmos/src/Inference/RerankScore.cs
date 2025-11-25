//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    /// <summary>
    /// Represents the score assigned to a document after a reranking operation.
    /// </summary>
#if PREVIEW
    public
#else
    internal
#endif

    class RerankScore
    {
        /// <summary>
        /// Gets the document content or identifier that was reranked.
        /// </summary>
        public object Document { get; }

        /// <summary>
        /// Gets the score assigned to the document after reranking.
        /// </summary>
        public double Score { get; }

        /// <summary>
        /// Gets the original index or position of the document before reranking.
        /// </summary>
        public int Index { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="RerankScore"/> class.
        /// </summary>
        /// <param name="document">The document content or identifier.</param>
        /// <param name="score">The reranked score for the document.</param>
        /// <param name="index">The original index of the document.</param>
        public RerankScore(object document, double score, int index)
        {
            this.Document = document;
            this.Score = score;
            this.Index = index;
        }
    }
}
