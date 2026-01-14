//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Custom.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Tests for the production CosmosDiagnosticsContext that verify Activity integration.
    /// These tests directly test the production code's OpenTelemetry Activity creation and disposal.
    /// </summary>
    [TestClass]
    public class CosmosDiagnosticsContextProductionTests
    {
        [TestMethod]
        public void Create_WithNullOptions_ReturnsValidContext()
        {
            // Act
            CosmosDiagnosticsContext context = CosmosDiagnosticsContext.Create(null);

            // Assert
            Assert.IsNotNull(context);
        }

        [TestMethod]
        public void CreateScope_WithActivityListener_CreatesActivity()
        {
            // Arrange
            CosmosDiagnosticsContext context = CosmosDiagnosticsContext.Create(null);
            List<Activity> capturedActivities = new List<Activity>();
            
            using ActivityListener listener = new ActivityListener
            {
                ShouldListenTo = (activitySource) => activitySource.Name == "Microsoft.Azure.Cosmos.Encryption.Custom",
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStarted = activity => { lock (capturedActivities) { capturedActivities.Add(activity); } }
            };
            ActivitySource.AddActivityListener(listener);

            // Act
            using (context.CreateScope("TestActivity"))
            {
                // Activity should be active
            }

            // Assert
            lock (capturedActivities)
            {
                Assert.AreEqual(1, capturedActivities.Count, "Expected one Activity to be created");
                Assert.AreEqual("TestActivity", capturedActivities[0].DisplayName);
                Assert.AreEqual(ActivityKind.Internal, capturedActivities[0].Kind);
            }
        }

        [TestMethod]
        public void CreateScope_WithoutActivityListener_DoesNotThrow()
        {
            // Arrange
            CosmosDiagnosticsContext context = CosmosDiagnosticsContext.Create(null);

            // Act & Assert - Should not throw even without a listener
            using (context.CreateScope("NoListenerScope"))
            {
                // No exception should be thrown
            }
        }

        [TestMethod]
        public void Scope_Dispose_DisposesActivity()
        {
            // Arrange
            CosmosDiagnosticsContext context = CosmosDiagnosticsContext.Create(null);
            Activity stoppedActivity = null;
            
            using ActivityListener listener = new ActivityListener
            {
                ShouldListenTo = (activitySource) => activitySource.Name == "Microsoft.Azure.Cosmos.Encryption.Custom",
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStopped = activity => stoppedActivity = activity
            };
            ActivitySource.AddActivityListener(listener);

            // Act
            using (CosmosDiagnosticsContext.Scope scope = context.CreateScope("DisposableActivity"))
            {
                // Activity created
            } // Activity should be disposed here

            // Assert
            Assert.IsNotNull(stoppedActivity, "Activity should have been stopped/disposed");
            Assert.AreEqual("DisposableActivity", stoppedActivity.DisplayName);
        }

        [TestMethod]
        public void Scope_DisposeIdempotent_OnlyDisposesActivityOnce()
        {
            // Arrange
            CosmosDiagnosticsContext context = CosmosDiagnosticsContext.Create(null);
            int stopCount = 0;
            
            using ActivityListener listener = new ActivityListener
            {
                ShouldListenTo = (activitySource) => activitySource.Name == "Microsoft.Azure.Cosmos.Encryption.Custom",
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStopped = activity => Interlocked.Increment(ref stopCount)
            };
            ActivitySource.AddActivityListener(listener);

            // Act
            CosmosDiagnosticsContext.Scope scope = context.CreateScope("IdempotentActivity");
            scope.Dispose();
            scope.Dispose();
            scope.Dispose();

            // Assert
            Assert.AreEqual(1, stopCount, "Activity should only be stopped once despite multiple Dispose calls");
        }

        [TestMethod]
        public void CreateScope_WithNullScope_ThrowsArgumentNullException()
        {
            // Arrange
            CosmosDiagnosticsContext context = CosmosDiagnosticsContext.Create(null);

            // Act & Assert
            ArgumentNullException ex = Assert.ThrowsException<ArgumentNullException>(() =>
            {
                context.CreateScope(null);
            });

            Assert.AreEqual("scope", ex.ParamName);
        }

        [TestMethod]
        public void CreateScope_WithEmptyScope_ThrowsArgumentException()
        {
            // Arrange
            CosmosDiagnosticsContext context = CosmosDiagnosticsContext.Create(null);

            // Act & Assert
            ArgumentException ex = Assert.ThrowsException<ArgumentException>(() =>
            {
                context.CreateScope(string.Empty);
            });

            Assert.AreEqual("scope", ex.ParamName);
        }

        [TestMethod]
        public void CreateScope_WithWhitespaceScope_ThrowsArgumentException()
        {
            // Arrange
            CosmosDiagnosticsContext context = CosmosDiagnosticsContext.Create(null);

            // Act & Assert
            ArgumentException ex = Assert.ThrowsException<ArgumentException>(() =>
            {
                context.CreateScope("   ");
            });

            Assert.AreEqual("scope", ex.ParamName);
        }

        [TestMethod]
        public void NestedScopes_CreatesNestedActivities()
        {
            // Arrange
            CosmosDiagnosticsContext context = CosmosDiagnosticsContext.Create(null);
            List<string> activityNames = new List<string>();
            
            using ActivityListener listener = new ActivityListener
            {
                ShouldListenTo = (activitySource) => activitySource.Name == "Microsoft.Azure.Cosmos.Encryption.Custom",
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStarted = activity => { lock (activityNames) { activityNames.Add(activity.DisplayName); } }
            };
            ActivitySource.AddActivityListener(listener);

            // Act
            using (context.CreateScope("Outer"))
            {
                using (context.CreateScope("Inner"))
                {
                    // Nested scope
                }
            }

            // Assert
            lock (activityNames)
            {
                Assert.AreEqual(2, activityNames.Count);
                Assert.AreEqual("Outer", activityNames[0]);
                Assert.AreEqual("Inner", activityNames[1]);
            }
        }

        [TestMethod]
        public void ConcurrentScopes_CreateIndependentActivities()
        {
            // Arrange
            CosmosDiagnosticsContext context1 = CosmosDiagnosticsContext.Create(null);
            CosmosDiagnosticsContext context2 = CosmosDiagnosticsContext.Create(null);
            List<string> activityNames = new List<string>();
            
            using ActivityListener listener = new ActivityListener
            {
                ShouldListenTo = (activitySource) => activitySource.Name == "Microsoft.Azure.Cosmos.Encryption.Custom",
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStarted = activity => { lock (activityNames) { activityNames.Add(activity.DisplayName); } }
            };
            ActivitySource.AddActivityListener(listener);

            // Act
            using (context1.CreateScope("Context1Scope"))
            {
            }
            using (context2.CreateScope("Context2Scope"))
            {
            }

            // Assert
            lock (activityNames)
            {
                Assert.AreEqual(2, activityNames.Count);
                Assert.IsTrue(activityNames.Contains("Context1Scope"));
                Assert.IsTrue(activityNames.Contains("Context2Scope"));
            }
        }

        [TestMethod]
        public void ScopePrefixes_CreateCorrectActivityNames()
        {
            // Arrange
            CosmosDiagnosticsContext context = CosmosDiagnosticsContext.Create(null);
            List<string> activityNames = new List<string>();
            
            using ActivityListener listener = new ActivityListener
            {
                ShouldListenTo = (activitySource) => activitySource.Name == "Microsoft.Azure.Cosmos.Encryption.Custom",
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStarted = activity => { lock (activityNames) { activityNames.Add(activity.DisplayName); } }
            };
            ActivitySource.AddActivityListener(listener);

            // Act
            string encryptScope = CosmosDiagnosticsContext.ScopeEncryptModeSelectionPrefix + "Stream";
            string decryptScope = CosmosDiagnosticsContext.ScopeDecryptModeSelectionPrefix + "Newtonsoft";
            
            using (context.CreateScope(encryptScope))
            {
            }
            using (context.CreateScope(decryptScope))
            {
            }

            // Assert
            lock (activityNames)
            {
                Assert.AreEqual(2, activityNames.Count);
                Assert.AreEqual("EncryptionProcessor.Encrypt.Mde.Stream", activityNames[0]);
                Assert.AreEqual("EncryptionProcessor.Decrypt.Mde.Newtonsoft", activityNames[1]);
            }
        }

        [TestMethod]
        public void MultipleScopes_WithSameName_EachCreatesActivity()
        {
            // Arrange
            CosmosDiagnosticsContext context = CosmosDiagnosticsContext.Create(null);
            int activityCount = 0;
            
            using ActivityListener listener = new ActivityListener
            {
                ShouldListenTo = (activitySource) => activitySource.Name == "Microsoft.Azure.Cosmos.Encryption.Custom",
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStarted = activity => Interlocked.Increment(ref activityCount)
            };
            ActivitySource.AddActivityListener(listener);

            // Act
            using (context.CreateScope("RepeatedScope"))
            {
            }
            using (context.CreateScope("RepeatedScope"))
            {
            }
            using (context.CreateScope("RepeatedScope"))
            {
            }

            // Assert
            Assert.AreEqual(3, activityCount, "Each CreateScope call should create a new Activity");
        }

        [TestMethod]
        public void Scope_WithException_StillDisposesActivity()
        {
            // Arrange
            CosmosDiagnosticsContext context = CosmosDiagnosticsContext.Create(null);
            Activity stoppedActivity = null;
            
            using ActivityListener listener = new ActivityListener
            {
                ShouldListenTo = (activitySource) => activitySource.Name == "Microsoft.Azure.Cosmos.Encryption.Custom",
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStopped = activity => stoppedActivity = activity
            };
            ActivitySource.AddActivityListener(listener);

            // Act & Assert
            try
            {
                using (context.CreateScope("ExceptionScope"))
                {
                    throw new InvalidOperationException("Test exception");
                }
            }
            catch (InvalidOperationException)
            {
                // Expected
            }

            // Assert - Activity should still be disposed even with exception
            Assert.IsNotNull(stoppedActivity, "Activity should be disposed even when exception occurs");
            Assert.AreEqual("ExceptionScope", stoppedActivity.DisplayName);
        }
    }
}
