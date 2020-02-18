//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query
{
    using System;
    using System.Net;
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
            QueryException queryException = new MalformedContinuationTokenException();
            TryCatch<object>  tryCatch = TryCatch<object>.FromException(queryException);
            QueryResponseCore queryResponse = QueryResponseFactory.CreateFromException(tryCatch.Exception);
            Assert.AreEqual(HttpStatusCode.BadRequest, queryResponse.StatusCode);
            Assert.IsNotNull(queryResponse.CosmosException);
            Assert.IsTrue(queryResponse.CosmosException.ToString().Contains(nameof(ExceptionFromTryCatch)));
        }
    }
}
