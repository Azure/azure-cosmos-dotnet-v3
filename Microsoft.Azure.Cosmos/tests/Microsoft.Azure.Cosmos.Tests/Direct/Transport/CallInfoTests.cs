//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Direct.Test
{
    using System;
    using System.Reflection;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Rntbd;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    [TestClass]
    public class CallInfoTests

    {
        [TestMethod]
        [Owner("askagarw")]
        public async Task SetResponseCompletesTaskTest()
        {
            using (Dispatcher.CallInfo callInfo = this.GetCallInfoObject())
            {
                Task completionTask = callInfo.ReadResponseAsync(new ChannelCallArguments(Guid.NewGuid()));
                Assert.IsFalse(completionTask.IsCompleted);
                callInfo.SetResponse(new Connection.ResponseMetadata(new BufferProvider.DisposableBuffer(), new BufferProvider.DisposableBuffer()),
                                    new TransportSerialization.RntbdHeader(StatusCodes.Accepted, Guid.NewGuid()),
                                    new System.IO.MemoryStream(),
                                    string.Empty,
                                    metadata: new byte[0],
                                    metadataLength: 0);
                await completionTask;
                Assert.IsTrue(completionTask.IsCompleted);
            }
        }
        [TestMethod]
        [Owner("askagarw")]
        public async Task WaitOnBrokenConnectionTest()
        {
            using (Dispatcher.CallInfo callInfo = this.GetCallInfoObject())
            {
                Exception innerException = new Exception("Inner Exception");
                callInfo.SetConnectionBrokenException(innerException, "Source Description");
                await Task.Delay(100);

                // the completion task should not complete as it should wait for send to complete
                Task completionTask = this.GetCompletionTaskAsync(callInfo);
                Assert.IsFalse(completionTask.IsCompleted);
                Task readResponseTask = callInfo.ReadResponseAsync(new ChannelCallArguments(Guid.NewGuid()));
                Assert.IsTrue(object.ReferenceEquals(readResponseTask, completionTask));
                TransportException ex = await Assert.ThrowsExceptionAsync<TransportException>(() => completionTask);
                Assert.AreEqual(ex.InnerException, innerException);
            }
        }
        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        [Owner("askagarw")]
        public void MultipleSendCompleteThrowsTest()
        {
            using (Dispatcher.CallInfo callInfo = this.GetCallInfoObject())
            {
                callInfo.ReadResponseAsync(new ChannelCallArguments(Guid.NewGuid()));
                // The second call should throw because the send has been complete already
                callInfo.ReadResponseAsync(new ChannelCallArguments(Guid.NewGuid()));
            }
        }

        private Dispatcher.CallInfo GetCallInfoObject()
        {
            return new Dispatcher.CallInfo(Guid.NewGuid(),
                                            Guid.NewGuid(),
                                           new Uri("http://someUri.com"),
                                           TaskScheduler.Current,
                                           new TransportRequestStats());
        }
        private Task GetCompletionTaskAsync(Dispatcher.CallInfo callInfo)
        {
            TaskCompletionSource<StoreResponse> completion = (TaskCompletionSource<StoreResponse>)(typeof(Dispatcher.CallInfo)
                                                                .GetField("completion", BindingFlags.NonPublic | BindingFlags.Instance)
                                                                .GetValue(callInfo));
            return completion.Task;
        }
    }
}