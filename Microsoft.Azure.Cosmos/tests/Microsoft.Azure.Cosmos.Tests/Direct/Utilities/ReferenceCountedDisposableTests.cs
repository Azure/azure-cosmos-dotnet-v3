// Based on https://github.com/dotnet/roslyn/blob/38e1b12b27bbe15ed5e0da3f90ea73e025ff5c7b/src/Workspaces/CoreTest/WorkspaceServiceTests/ReferenceCountedDisposableTests.cs
// but with WeakReference functionality removed, and adapted for mstest instead of xunit.

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable disable


namespace Microsoft.Azure.Documents.Client.Tests
{
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using System;
    using System.Reflection;

    [TestClass]
    public class ReferenceCountedDisposableTests
    {
        [TestMethod]
        [Owner("kevinpi")]
        public void TestArgumentValidation()
            => Assert.ThrowsException<ArgumentNullException>(() => new ReferenceCountedDisposable<IDisposable>(null));

        [DataTestMethod]
        [DataRow(1)]
        [DataRow(3)]
        [Owner("kevinpi")]
        public void TestSingleReferenceDispose(int disposeCount)
        {
            var target = new DisposableObject();

            var reference = new ReferenceCountedDisposable<DisposableObject>(target);
            Assert.AreSame(target, reference.Target);
            Assert.IsFalse(target.IsDisposed);
            Assert.AreEqual(0, target.DisposeCount);

            for (var i = 0; i < disposeCount; i++)
            {
                reference.Dispose();
            }

            Assert.ThrowsException<ObjectDisposedException>(() => reference.Target);
            Assert.IsTrue(target.IsDisposed);
            Assert.AreEqual(1, target.DisposeCount);
        }

        [TestMethod]
        [Owner("kevinpi")]
        public void TestTryAddReferenceFailsAfterDispose()
        {
            var target = new DisposableObject();

            var reference = new ReferenceCountedDisposable<DisposableObject>(target);
            reference.Dispose();

            Assert.IsNull(reference.TryAddReference());
        }

        [TestMethod]
        [Owner("kevinpi")]
        public void TestTryAddReferenceFailsAfterDispose2()
        {
            var target = new DisposableObject();

            var reference = new ReferenceCountedDisposable<DisposableObject>(target);

            // TryAddReference succeeds before dispose
            var reference2 = reference.TryAddReference();
            Assert.IsNotNull(reference2);

            reference.Dispose();

            // TryAddReference fails after dispose, even if another instance is alive
            Assert.IsNull(reference.TryAddReference());
            Assert.IsNotNull(reference2.Target);
            Assert.IsFalse(target.IsDisposed);
        }

        [TestMethod]
        [Owner("kevinpi")]
        public void TestOutOfOrderDispose()
        {
            var target = new DisposableObject();

            var reference = new ReferenceCountedDisposable<DisposableObject>(target);
            var reference2 = reference.TryAddReference();
            var reference3 = reference2.TryAddReference();

            reference2.Dispose();
            Assert.IsFalse(target.IsDisposed);

            reference3.Dispose();
            Assert.IsFalse(target.IsDisposed);

            reference.Dispose();
            Assert.IsTrue(target.IsDisposed);
            Assert.AreEqual(1, target.DisposeCount);
        }

        private sealed class DisposableObject : IDisposable
        {
            public bool IsDisposed
            {
                get;
                private set;
            }

            public int DisposeCount
            {
                get;
                private set;
            }

            public void Dispose()
            {
                IsDisposed = true;
                DisposeCount++;
            }
        }
    }
}