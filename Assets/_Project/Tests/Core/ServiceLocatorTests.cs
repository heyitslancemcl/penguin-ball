using NUnit.Framework;
using PenguineBall.Core;
using System;

namespace PenguineBall.Tests.Core
{
    /// <summary>
    /// Unity Test Runner (EditMode) tests for ServiceLocator.
    /// Run via: Window → General → Test Runner → EditMode.
    /// </summary>
    public class ServiceLocatorTests
    {
        [TearDown]
        public void TearDown()
        {
            ServiceLocator.Clear();
        }

        // ── Register / Get ────────────────────────────────────────────────────────

        [Test]
        public void Get_ReturnsRegisteredInstance()
        {
            var stub = new StubService();
            ServiceLocator.Register<IStubService>(stub);

            var result = ServiceLocator.Get<IStubService>();

            Assert.That(result, Is.SameAs(stub));
        }

        [Test]
        public void Register_OverwritesPreviousRegistration()
        {
            var first  = new StubService();
            var second = new StubService();
            ServiceLocator.Register<IStubService>(first);
            ServiceLocator.Register<IStubService>(second);

            var result = ServiceLocator.Get<IStubService>();

            Assert.That(result, Is.SameAs(second));
        }

        [Test]
        public void Get_ThrowsInvalidOperationException_WhenNotRegistered()
        {
            Assert.Throws<InvalidOperationException>(() => ServiceLocator.Get<IStubService>());
        }

        [Test]
        public void Get_ExceptionMessage_ContainsTypeName()
        {
            var ex = Assert.Throws<InvalidOperationException>(() => ServiceLocator.Get<IStubService>());

            Assert.That(ex.Message, Does.Contain(nameof(IStubService)));
        }

        [Test]
        public void Clear_RemovesAllRegistrations()
        {
            ServiceLocator.Register<IStubService>(new StubService());
            ServiceLocator.Clear();

            Assert.Throws<InvalidOperationException>(() => ServiceLocator.Get<IStubService>());
        }

        // ── Test doubles ──────────────────────────────────────────────────────────

        private interface IStubService { }
        private class StubService : IStubService { }
    }
}
