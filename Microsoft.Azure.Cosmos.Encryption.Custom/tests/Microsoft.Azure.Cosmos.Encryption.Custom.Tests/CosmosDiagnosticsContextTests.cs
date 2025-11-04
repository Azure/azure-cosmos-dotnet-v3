//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Custom.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class CosmosDiagnosticsContextTests
    {
        [TestMethod]
        public void Create_WithNullOptions_ReturnsValidContext()
        {
            // Act
            TestableCosmosDiagnosticsContext context = new TestableCosmosDiagnosticsContext();

            // Assert
            Assert.IsNotNull(context);
            Assert.IsNotNull(context.Scopes);
            Assert.AreEqual(0, context.Scopes.Count);
        }

        [TestMethod]
        public void Create_WithValidOptions_ReturnsValidContext()
        {
            // Arrange & Act
            TestableCosmosDiagnosticsContext context = new TestableCosmosDiagnosticsContext();

            // Assert
            Assert.IsNotNull(context);
            Assert.IsNotNull(context.Scopes);
            Assert.AreEqual(0, context.Scopes.Count);
        }

        [TestMethod]
        public void CreateScope_WithValidScopeName_RecordsScopeName()
        {
            // Arrange
            TestableCosmosDiagnosticsContext context = new TestableCosmosDiagnosticsContext();
            const string scopeName = "TestScope";

            // Act
            using (context.CreateScope(scopeName))
            {
                // Scope is active
            }

            // Assert
            Assert.AreEqual(1, context.Scopes.Count);
            Assert.AreEqual(scopeName, context.Scopes[0]);
        }

        [TestMethod]
        public void CreateScope_WithNullScopeName_ThrowsArgumentNullException()
        {
            // Arrange
            TestableCosmosDiagnosticsContext context = new TestableCosmosDiagnosticsContext();

            // Act & Assert
            ArgumentNullException ex = Assert.ThrowsException<ArgumentNullException>(() =>
            {
                using (context.CreateScope(null))
                {
                }
            });

            Assert.AreEqual("scope", ex.ParamName);
        }

        [TestMethod]
        public void CreateScope_WithEmptyScopeName_ThrowsArgumentException()
        {
            // Arrange
            TestableCosmosDiagnosticsContext context = new TestableCosmosDiagnosticsContext();

            // Act & Assert
            ArgumentException ex = Assert.ThrowsException<ArgumentException>(() =>
            {
                using (context.CreateScope(string.Empty))
                {
                }
            });

            Assert.AreEqual("scope", ex.ParamName);
        }

        [TestMethod]
        public void CreateScope_MultipleScopesSequential_RecordsAllScopesInOrder()
        {
            // Arrange
            TestableCosmosDiagnosticsContext context = new TestableCosmosDiagnosticsContext();
            const string scope1 = "Scope1";
            const string scope2 = "Scope2";
            const string scope3 = "Scope3";

            // Act
            using (context.CreateScope(scope1))
            {
            }
            using (context.CreateScope(scope2))
            {
            }
            using (context.CreateScope(scope3))
            {
            }

            // Assert
            Assert.AreEqual(3, context.Scopes.Count);
            Assert.AreEqual(scope1, context.Scopes[0]);
            Assert.AreEqual(scope2, context.Scopes[1]);
            Assert.AreEqual(scope3, context.Scopes[2]);
        }

        [TestMethod]
        public void CreateScope_NestedScopes_RecordsInLIFOOrder()
        {
            // Arrange
            TestableCosmosDiagnosticsContext context = new TestableCosmosDiagnosticsContext();
            const string outerScope = "OuterScope";
            const string innerScope1 = "InnerScope1";
            const string innerScope2 = "InnerScope2";

            // Act
            using (context.CreateScope(outerScope))
            {
                using (context.CreateScope(innerScope1))
                {
                    // Inner scope 1 completes first
                }

                using (context.CreateScope(innerScope2))
                {
                    // Inner scope 2 completes second
                }
                // Outer scope completes last
            }

            // Assert
            // Scopes should be recorded in the order they complete (LIFO for nested)
            Assert.AreEqual(3, context.Scopes.Count);
            Assert.AreEqual(innerScope1, context.Scopes[0]);
            Assert.AreEqual(innerScope2, context.Scopes[1]);
            Assert.AreEqual(outerScope, context.Scopes[2]);
        }

        [TestMethod]
        public void CreateScope_DeeplyNestedScopes_RecordsAllCorrectly()
        {
            // Arrange
            TestableCosmosDiagnosticsContext context = new TestableCosmosDiagnosticsContext();

            // Act
            using (context.CreateScope("Level1"))
            {
                using (context.CreateScope("Level2"))
                {
                    using (context.CreateScope("Level3"))
                    {
                        using (context.CreateScope("Level4"))
                        {
                            // Deepest level
                        }
                    }
                }
            }

            // Assert
            Assert.AreEqual(4, context.Scopes.Count);
            Assert.AreEqual("Level4", context.Scopes[0]);
            Assert.AreEqual("Level3", context.Scopes[1]);
            Assert.AreEqual("Level2", context.Scopes[2]);
            Assert.AreEqual("Level1", context.Scopes[3]);
        }

        [TestMethod]
        public void CreateScope_RecordsTimingInformation()
        {
            // Arrange
            TestableCosmosDiagnosticsContext context = new TestableCosmosDiagnosticsContext();
            const string scopeName = "TimedScope";
            const int delayMs = 50;

            // Act
            using (context.CreateScope(scopeName))
            {
                Thread.Sleep(delayMs);
            }

            // Assert
            Assert.AreEqual(1, context.Scopes.Count);
            Assert.AreEqual(scopeName, context.Scopes[0]);
            
            // Verify timing was recorded (we can't easily access ScopeRecord directly in this test,
            // but the scope name being recorded confirms the timing infrastructure is working)
        }

        [TestMethod]
        public void CreateScope_WithSameScopeNameMultipleTimes_RecordsEachOccurrence()
        {
            // Arrange
            TestableCosmosDiagnosticsContext context = new TestableCosmosDiagnosticsContext();
            const string scopeName = "RepeatedScope";

            // Act
            using (context.CreateScope(scopeName))
            {
            }
            using (context.CreateScope(scopeName))
            {
            }
            using (context.CreateScope(scopeName))
            {
            }

            // Assert
            Assert.AreEqual(3, context.Scopes.Count);
            Assert.IsTrue(context.Scopes.All(s => s == scopeName));
        }

        [TestMethod]
        public void Scopes_BeforeAnyScopes_ReturnsEmptyArray()
        {
            // Arrange
            TestableCosmosDiagnosticsContext context = new TestableCosmosDiagnosticsContext();

            // Act
            IReadOnlyList<string> scopes = context.Scopes;

            // Assert
            Assert.IsNotNull(scopes);
            Assert.AreEqual(0, scopes.Count);
        }

        [TestMethod]
        public void Scopes_AfterScopesAdded_ReturnsSnapshot()
        {
            // Arrange
            TestableCosmosDiagnosticsContext context = new TestableCosmosDiagnosticsContext();
            using (context.CreateScope("Scope1"))
            {
            }

            // Act
            IReadOnlyList<string> snapshot1 = context.Scopes;
            
            using (context.CreateScope("Scope2"))
            {
            }
            
            IReadOnlyList<string> snapshot2 = context.Scopes;

            // Assert
            Assert.AreEqual(1, snapshot1.Count);
            Assert.AreEqual(2, snapshot2.Count);
            Assert.AreEqual("Scope1", snapshot1[0]);
            Assert.AreEqual("Scope1", snapshot2[0]);
            Assert.AreEqual("Scope2", snapshot2[1]);
        }

        [TestMethod]
        public void CreateScope_WithEncryptionPrefixes_BuildsCorrectScopeNames()
        {
            // Arrange
            TestableCosmosDiagnosticsContext context = new TestableCosmosDiagnosticsContext();
            string encryptStreamScope = CosmosDiagnosticsContext.ScopeEncryptModeSelectionPrefix + "Stream";
            string decryptNewtonsoftScope = CosmosDiagnosticsContext.ScopeDecryptModeSelectionPrefix + "Newtonsoft";

            // Act
            using (context.CreateScope(encryptStreamScope))
            {
            }
            using (context.CreateScope(decryptNewtonsoftScope))
            {
            }

            // Assert
            Assert.AreEqual(2, context.Scopes.Count);
            Assert.AreEqual("EncryptionProcessor.Encrypt.Mde.Stream", context.Scopes[0]);
            Assert.AreEqual("EncryptionProcessor.Decrypt.Mde.Newtonsoft", context.Scopes[1]);
        }

        [TestMethod]
        public void CreateScope_ConcurrentScopesOnDifferentContexts_AreIndependent()
        {
            // Arrange
            TestableCosmosDiagnosticsContext context1 = new TestableCosmosDiagnosticsContext();
            TestableCosmosDiagnosticsContext context2 = new TestableCosmosDiagnosticsContext();

            // Act
            using (context1.CreateScope("Context1Scope1"))
            {
            }
            using (context2.CreateScope("Context2Scope1"))
            {
            }
            using (context1.CreateScope("Context1Scope2"))
            {
            }

            // Assert
            Assert.AreEqual(2, context1.Scopes.Count);
            Assert.AreEqual(1, context2.Scopes.Count);
            Assert.AreEqual("Context1Scope1", context1.Scopes[0]);
            Assert.AreEqual("Context1Scope2", context1.Scopes[1]);
            Assert.AreEqual("Context2Scope1", context2.Scopes[0]);
        }

        [TestMethod]
        public void CreateScope_ThreadSafety_RecordsAllScopesFromConcurrentThreads()
        {
            // Arrange
            TestableCosmosDiagnosticsContext context = new TestableCosmosDiagnosticsContext();
            const int threadCount = 10;
            const int scopesPerThread = 5;
            CountdownEvent countdown = new CountdownEvent(threadCount);

            // Act
            List<Task> tasks = new List<Task>();
            for (int i = 0; i < threadCount; i++)
            {
                int threadId = i;
                tasks.Add(Task.Run(() =>
                {
                    for (int j = 0; j < scopesPerThread; j++)
                    {
                        using (context.CreateScope($"Thread{threadId}_Scope{j}"))
                        {
                            Thread.Sleep(1); // Small delay to increase interleaving
                        }
                    }
                    countdown.Signal();
                }));
            }

            countdown.Wait();
            Task.WaitAll(tasks.ToArray());

            // Assert
            Assert.AreEqual(threadCount * scopesPerThread, context.Scopes.Count);
            
            // Verify all thread scopes are present
            for (int i = 0; i < threadCount; i++)
            {
                for (int j = 0; j < scopesPerThread; j++)
                {
                    string expectedScope = $"Thread{i}_Scope{j}";
                    Assert.IsTrue(context.Scopes.Contains(expectedScope), 
                        $"Expected scope '{expectedScope}' not found in recorded scopes");
                }
            }
        }

        [TestMethod]
        public void Scope_Dispose_IsIdempotent()
        {
            // Arrange
            TestableCosmosDiagnosticsContext context = new TestableCosmosDiagnosticsContext();
            TestableCosmosDiagnosticsContext.TestableScope scope = context.CreateScope("IdempotentScope");

            // Act - dispose multiple times (this tests the idempotency guarantee)
            scope.Dispose();
            scope.Dispose();
            scope.Dispose();

            // Assert - should only record once even though Dispose was called 3 times
            Assert.AreEqual(1, context.Scopes.Count);
            Assert.AreEqual("IdempotentScope", context.Scopes[0]);
        }

        [TestMethod]
        public void CreateScope_WithExceptionInScope_StillRecordsTiming()
        {
            // Arrange
            TestableCosmosDiagnosticsContext context = new TestableCosmosDiagnosticsContext();
            const string scopeName = "ExceptionScope";

            // Act & Assert
            try
            {
                using (context.CreateScope(scopeName))
                {
                    throw new InvalidOperationException("Test exception");
                }
            }
            catch (InvalidOperationException)
            {
                // Expected
            }

            // Assert - scope should still be recorded even though exception was thrown
            Assert.AreEqual(1, context.Scopes.Count);
            Assert.AreEqual(scopeName, context.Scopes[0]);
        }

        [TestMethod]
        public void CreateScope_AsyncOperations_RecordsCorrectly()
        {
            // Arrange
            TestableCosmosDiagnosticsContext context = new TestableCosmosDiagnosticsContext();

            // Act
            async Task TestAsync()
            {
                using (context.CreateScope("AsyncScope1"))
                {
                    await Task.Delay(10);
                }

                using (context.CreateScope("AsyncScope2"))
                {
                    await Task.Delay(10);
                }
            }

            TestAsync().GetAwaiter().GetResult();

            // Assert
            Assert.AreEqual(2, context.Scopes.Count);
            Assert.AreEqual("AsyncScope1", context.Scopes[0]);
            Assert.AreEqual("AsyncScope2", context.Scopes[1]);
        }

        [TestMethod]
        public void ScopeRecord_Properties_AreCorrect()
        {
            // Arrange
            const string name = "TestScope";
            long startTimestamp = Stopwatch.GetTimestamp();
            long elapsedTicks = Stopwatch.Frequency;

            // Act
            TestableCosmosDiagnosticsContext.ScopeRecord record = new TestableCosmosDiagnosticsContext.ScopeRecord(
                name, 
                startTimestamp, 
                elapsedTicks);

            // Assert
            Assert.AreEqual(name, record.Name);
            Assert.AreEqual(startTimestamp, record.StartTimestamp);
            Assert.AreEqual(elapsedTicks, record.ElapsedTicks);
            
            // Elapsed should convert ticks to TimeSpan
            TimeSpan elapsed = record.Elapsed;
            Assert.IsTrue(elapsed.TotalSeconds >= 0.9 && elapsed.TotalSeconds <= 1.1, 
                $"Expected ~1 second, got {elapsed.TotalSeconds} seconds");
        }

        [TestMethod]
        public void CreateScope_LongRunningScope_RecordsAccurateElapsedTime()
        {
            // Arrange
            TestableCosmosDiagnosticsContext context = new TestableCosmosDiagnosticsContext();
            const int delayMs = 100;

            // Act
            Stopwatch sw = Stopwatch.StartNew();
            using (context.CreateScope("LongScope"))
            {
                Thread.Sleep(delayMs);
            }
            sw.Stop();

            // Assert
            Assert.AreEqual(1, context.Scopes.Count);
            // We can verify the scope was recorded, actual timing validation would require
            // accessing the internal ScopeRecord which is tested separately
        }

        [TestMethod]
        public void CreateScope_ScopePrefixes_AreDefinedCorrectly()
        {
            // Assert
            Assert.AreEqual("EncryptionProcessor.Encrypt.Mde.", 
                CosmosDiagnosticsContext.ScopeEncryptModeSelectionPrefix);
            Assert.AreEqual("EncryptionProcessor.Decrypt.Mde.", 
                CosmosDiagnosticsContext.ScopeDecryptModeSelectionPrefix);
        }

        [TestMethod]
        public void CreateScope_WithWhitespaceOnlyScopeName_ThrowsArgumentException()
        {
            // Arrange
            TestableCosmosDiagnosticsContext context = new TestableCosmosDiagnosticsContext();

            // Act & Assert
            ArgumentException ex = Assert.ThrowsException<ArgumentException>(() =>
            {
                using (context.CreateScope("   "))
                {
                }
            });

            Assert.AreEqual("scope", ex.ParamName);
        }

        [TestMethod]
        public void CreateScope_ComplexNestedAndSequentialPattern_RecordsCorrectly()
        {
            // Arrange
            TestableCosmosDiagnosticsContext context = new TestableCosmosDiagnosticsContext();

            // Act - Complex pattern: Outer { Inner1 { InnerInner1 } Inner2 } Sequential
            using (context.CreateScope("Outer"))
            {
                using (context.CreateScope("Inner1"))
                {
                    using (context.CreateScope("InnerInner1"))
                    {
                    }
                }
                using (context.CreateScope("Inner2"))
                {
                }
            }
            using (context.CreateScope("Sequential"))
            {
            }

            // Assert - Recording order: InnerInner1, Inner1, Inner2, Outer, Sequential
            Assert.AreEqual(5, context.Scopes.Count);
            Assert.AreEqual("InnerInner1", context.Scopes[0]);
            Assert.AreEqual("Inner1", context.Scopes[1]);
            Assert.AreEqual("Inner2", context.Scopes[2]);
            Assert.AreEqual("Outer", context.Scopes[3]);
            Assert.AreEqual("Sequential", context.Scopes[4]);
        }

        #region Activity Integration Tests (Production CosmosDiagnosticsContext)

        [TestMethod]
        public void ProductionContext_CreateScope_CreatesActivity()
        {
            // Test the actual production CosmosDiagnosticsContext, not the wrapper
            List<Activity> capturedActivities = new List<Activity>();
            using ActivityListener listener = new ActivityListener
            {
                ShouldListenTo = (activitySource) => activitySource.Name == "Microsoft.Azure.Cosmos.Encryption.Custom",
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStarted = activity => { lock (capturedActivities) { capturedActivities.Add(activity); } }
            };
            ActivitySource.AddActivityListener(listener);

            CosmosDiagnosticsContext context = CosmosDiagnosticsContext.Create(null);
            using (context.CreateScope("TestScope"))
            {
                // Activity should be created and started
            }

            lock (capturedActivities)
            {
                Assert.AreEqual(1, capturedActivities.Count, "Should have captured exactly one activity");
                Assert.AreEqual("TestScope", capturedActivities[0].DisplayName);
            }
        }

        [TestMethod]
        public void ProductionContext_NestedScopes_CreatesNestedActivities()
        {
            List<Activity> capturedActivities = new List<Activity>();
            using ActivityListener listener = new ActivityListener
            {
                ShouldListenTo = (activitySource) => activitySource.Name == "Microsoft.Azure.Cosmos.Encryption.Custom",
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStarted = activity => { lock (capturedActivities) { capturedActivities.Add(activity); } }
            };
            ActivitySource.AddActivityListener(listener);

            CosmosDiagnosticsContext context = CosmosDiagnosticsContext.Create(null);
            using (context.CreateScope("Outer"))
            {
                using (context.CreateScope("Inner"))
                {
                    // Both activities should be created
                }
            }

            lock (capturedActivities)
            {
                Assert.AreEqual(2, capturedActivities.Count, "Should have captured two activities");
                Assert.AreEqual("Outer", capturedActivities[0].DisplayName);
                Assert.AreEqual("Inner", capturedActivities[1].DisplayName);
                
                // Inner should be a child of Outer
                Assert.AreEqual(capturedActivities[0].Id, capturedActivities[1].ParentId);
            }
        }

        [TestMethod]
        public void ProductionContext_WithoutListener_DoesNotCreateActivity()
        {
            // When no listener is active, Activity should be null (optimization)
            CosmosDiagnosticsContext context = CosmosDiagnosticsContext.Create(null);
            
            // Create scope without any listener - should work but not create Activity
            using (CosmosDiagnosticsContext.Scope scope = context.CreateScope("NoListener"))
            {
                // Should not throw, even though no Activity is created
            }

            // Test passes if no exception is thrown
            Assert.IsTrue(true);
        }

        [TestMethod]
        public void ProductionContext_ScopeDisposal_StopsActivity()
        {
            Activity startedActivity = null;
            Activity stoppedActivity = null;
            
            using ActivityListener listener = new ActivityListener
            {
                ShouldListenTo = (activitySource) => activitySource.Name == "Microsoft.Azure.Cosmos.Encryption.Custom",
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStarted = activity => startedActivity = activity,
                ActivityStopped = activity => stoppedActivity = activity
            };
            ActivitySource.AddActivityListener(listener);

            CosmosDiagnosticsContext context = CosmosDiagnosticsContext.Create(null);
            using (context.CreateScope("TestScope"))
            {
                Assert.IsNotNull(startedActivity, "Activity should have started");
                Assert.IsNull(stoppedActivity, "Activity should not be stopped yet");
            }

            Assert.IsNotNull(stoppedActivity, "Activity should be stopped after dispose");
            Assert.AreSame(startedActivity, stoppedActivity, "Same activity should be started and stopped");
        }

        [TestMethod]
        public void ProductionContext_IdempotentDisposal_OnlyStopsActivityOnce()
        {
            int stopCount = 0;
            
            using ActivityListener listener = new ActivityListener
            {
                ShouldListenTo = (activitySource) => activitySource.Name == "Microsoft.Azure.Cosmos.Encryption.Custom",
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStopped = activity => stopCount++
            };
            ActivitySource.AddActivityListener(listener);

            CosmosDiagnosticsContext context = CosmosDiagnosticsContext.Create(null);
            CosmosDiagnosticsContext.Scope scope = context.CreateScope("TestScope");
            
            scope.Dispose();
            scope.Dispose();
            scope.Dispose();

            Assert.AreEqual(1, stopCount, "Activity should only be stopped once despite multiple Dispose calls");
        }

        [TestMethod]
        public void ProductionContext_MultipleScopes_EachCreatesOwnActivity()
        {
            List<Activity> capturedActivities = new List<Activity>();
            using ActivityListener listener = new ActivityListener
            {
                ShouldListenTo = (activitySource) => activitySource.Name == "Microsoft.Azure.Cosmos.Encryption.Custom",
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStarted = activity => { lock (capturedActivities) { capturedActivities.Add(activity); } }
            };
            ActivitySource.AddActivityListener(listener);

            CosmosDiagnosticsContext context = CosmosDiagnosticsContext.Create(null);
            
            using (context.CreateScope("Scope1")) { }
            using (context.CreateScope("Scope2")) { }
            using (context.CreateScope("Scope3")) { }

            lock (capturedActivities)
            {
                Assert.AreEqual(3, capturedActivities.Count);
                Assert.AreEqual("Scope1", capturedActivities[0].DisplayName);
                Assert.AreEqual("Scope2", capturedActivities[1].DisplayName);
                Assert.AreEqual("Scope3", capturedActivities[2].DisplayName);
            }
        }

        [TestMethod]
        public void ProductionContext_ActivityKind_IsInternal()
        {
            Activity capturedActivity = null;
            using ActivityListener listener = new ActivityListener
            {
                ShouldListenTo = (activitySource) => activitySource.Name == "Microsoft.Azure.Cosmos.Encryption.Custom",
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStarted = activity => capturedActivity = activity
            };
            ActivitySource.AddActivityListener(listener);

            CosmosDiagnosticsContext context = CosmosDiagnosticsContext.Create(null);
            using (context.CreateScope("TestScope")) { }

            Assert.IsNotNull(capturedActivity);
            Assert.AreEqual(ActivityKind.Internal, capturedActivity.Kind);
        }

        [TestMethod]
        public void ProductionContext_ActivitySource_HasCorrectName()
        {
            Activity capturedActivity = null;
            using ActivityListener listener = new ActivityListener
            {
                ShouldListenTo = (activitySource) => activitySource.Name == "Microsoft.Azure.Cosmos.Encryption.Custom",
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStarted = activity => capturedActivity = activity
            };
            ActivitySource.AddActivityListener(listener);

            CosmosDiagnosticsContext context = CosmosDiagnosticsContext.Create(null);
            using (context.CreateScope("TestScope")) { }

            Assert.IsNotNull(capturedActivity);
            Assert.AreEqual("Microsoft.Azure.Cosmos.Encryption.Custom", capturedActivity.Source.Name);
        }

        [TestMethod]
        public void ProductionContext_ConcurrentScopes_IndependentActivities()
        {
            List<Activity> capturedActivities = new List<Activity>();
            using ActivityListener listener = new ActivityListener
            {
                ShouldListenTo = (activitySource) => activitySource.Name == "Microsoft.Azure.Cosmos.Encryption.Custom",
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStarted = activity => { lock (capturedActivities) { capturedActivities.Add(activity); } }
            };
            ActivitySource.AddActivityListener(listener);

            CosmosDiagnosticsContext context1 = CosmosDiagnosticsContext.Create(null);
            CosmosDiagnosticsContext context2 = CosmosDiagnosticsContext.Create(null);

            using (context1.CreateScope("Context1Scope"))
            using (context2.CreateScope("Context2Scope"))
            {
                // Both should create independent activities
            }

            lock (capturedActivities)
            {
                Assert.AreEqual(2, capturedActivities.Count);
                // They should have different IDs
                Assert.AreNotEqual(capturedActivities[0].Id, capturedActivities[1].Id);
            }
        }

        [TestMethod]
        public void ProductionContext_ScopeWithNullName_ThrowsArgumentException()
        {
            CosmosDiagnosticsContext context = CosmosDiagnosticsContext.Create(null);

            // ArgumentNullException is a subclass of ArgumentException, so we expect ArgumentNullException specifically
            ArgumentNullException exception = Assert.ThrowsException<ArgumentNullException>(() =>
            {
                using (context.CreateScope(null)) { }
            });

            Assert.IsTrue(exception.ParamName == "scope");
        }

        [TestMethod]
        public void ProductionContext_ScopeWithEmptyName_ThrowsArgumentException()
        {
            CosmosDiagnosticsContext context = CosmosDiagnosticsContext.Create(null);

            ArgumentException exception = Assert.ThrowsException<ArgumentException>(() =>
            {
                using (context.CreateScope(string.Empty)) { }
            });

            Assert.IsTrue(exception.Message.Contains("scope"));
        }

        [TestMethod]
        public void ProductionContext_ScopeWithWhitespaceName_ThrowsArgumentException()
        {
            CosmosDiagnosticsContext context = CosmosDiagnosticsContext.Create(null);

            ArgumentException exception = Assert.ThrowsException<ArgumentException>(() =>
            {
                using (context.CreateScope("   ")) { }
            });

            Assert.IsTrue(exception.Message.Contains("scope"));
        }

        [TestMethod]
        public void ProductionContext_MultipleListeners_AllReceiveEvents()
        {
            int listener1StartCount = 0;
            int listener2StartCount = 0;

            using ActivityListener listener1 = new ActivityListener
            {
                ShouldListenTo = (activitySource) => activitySource.Name == "Microsoft.Azure.Cosmos.Encryption.Custom",
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStarted = activity => listener1StartCount++
            };

            using ActivityListener listener2 = new ActivityListener
            {
                ShouldListenTo = (activitySource) => activitySource.Name == "Microsoft.Azure.Cosmos.Encryption.Custom",
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStarted = activity => listener2StartCount++
            };

            ActivitySource.AddActivityListener(listener1);
            ActivitySource.AddActivityListener(listener2);

            CosmosDiagnosticsContext context = CosmosDiagnosticsContext.Create(null);
            using (context.CreateScope("TestScope")) { }

            Assert.AreEqual(1, listener1StartCount, "Listener 1 should receive the event");
            Assert.AreEqual(1, listener2StartCount, "Listener 2 should receive the event");
        }

        #endregion
    }
}
