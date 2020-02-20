//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query
{
    using System;
    using System.Net;
    using System.Runtime.CompilerServices;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Cosmos.Query.Core.Exceptions;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.QueryClient;
    using Microsoft.Azure.Cosmos.Resource.CosmosExceptions;
    using VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class QueryResponseFactoryTests
    {
        [TestMethod]
        public void CosmosException()
        {
            CosmosException cosmosException = new CosmosBadRequestException(
                message: "asdf");
            QueryResponseCore queryResponse = QueryResponseFactory.CreateFromException(cosmosException);
            Assert.AreEqual(HttpStatusCode.BadRequest, queryResponse.StatusCode);
            Assert.IsNotNull(queryResponse.CosmosException);
        }

        [TestMethod]
        public void DocumentClientException()
        {
            Documents.DocumentClientException documentClientException = new Documents.RequestRateTooLargeException("asdf");
            QueryResponseCore queryResponse = QueryResponseFactory.CreateFromException(documentClientException);
            Assert.AreEqual((HttpStatusCode)429, queryResponse.StatusCode);
            Assert.IsNotNull(queryResponse.CosmosException);
        }

        [TestMethod]
        public void RandomException()
        {
            QueryResponseCore queryResponse = QueryResponseFactory.CreateFromException(new Exception());
            Assert.AreEqual(HttpStatusCode.InternalServerError, queryResponse.StatusCode);
        }

        [TestMethod]
        public void QueryException()
        {
            QueryException queryException = new MalformedContinuationTokenException();
            QueryResponseCore queryResponse = QueryResponseFactory.CreateFromException(queryException);
            Assert.AreEqual(HttpStatusCode.BadRequest, queryResponse.StatusCode);
            Assert.IsNotNull(queryResponse.CosmosException);
        }

        [TestMethod]
        public void ExceptionFromTryCatch()
        {
            TryCatch<object> tryCatch = this.QueryExceptionHelper(new MalformedContinuationTokenException("TestMessage"));
            QueryResponseCore queryResponse = QueryResponseFactory.CreateFromException(tryCatch.Exception);
            Assert.AreEqual(HttpStatusCode.BadRequest, queryResponse.StatusCode);
            Assert.IsNotNull(queryResponse.CosmosException);
            Assert.IsTrue(queryResponse.CosmosException.ToString().Contains("TestMessage"));
            Assert.IsTrue(queryResponse.CosmosException.ToString().Contains(nameof(QueryExceptionHelper)), queryResponse.CosmosException.ToString());
        }

        [TestMethod]
        public void ExceptionFromTryCatchWithCosmosException()
        {
            CosmosException cosmosException;
            try
            {
                throw new CosmosBadRequestException("InternalServerTestMessage");
            }
            catch (CosmosException ce)
            {
                cosmosException = ce;
            }

            TryCatch<object> tryCatch = this.QueryExceptionHelper(cosmosException);
            QueryResponseCore queryResponse = QueryResponseFactory.CreateFromException(tryCatch.Exception);
            Assert.AreEqual(HttpStatusCode.BadRequest, queryResponse.StatusCode);
            Assert.IsNotNull(queryResponse.CosmosException);

            // Should preserve the original stack trace.
            string exceptionMessage = queryResponse.CosmosException.ToString();
            Assert.IsTrue(exceptionMessage.Contains("InternalServerTestMessage"));
            Assert.IsFalse(exceptionMessage.Contains(nameof(QueryExceptionHelper)));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private TryCatch<object> QueryExceptionHelper(Exception exception)
        {
            return TryCatch<object>.FromException(exception);
        }
    }
}
