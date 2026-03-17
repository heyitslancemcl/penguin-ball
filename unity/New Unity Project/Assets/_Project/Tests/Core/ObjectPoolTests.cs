using NUnit.Framework;
using PenguineBall.Core;
using UnityEngine;
using UnityEngine.TestTools;
using System.Collections;

namespace PenguineBall.Tests.Core
{
    /// <summary>
    /// Unity Test Runner (PlayMode) tests for ObjectPool.
    /// PlayMode is required because ObjectPool instantiates MonoBehaviours.
    /// Run via: Window → General → Test Runner → PlayMode.
    /// </summary>
    public class ObjectPoolTests
    {
        private GameObject _prefabGO;
        private StubPoolable _prefab;

        [SetUp]
        public void SetUp()
        {
            _prefabGO = new GameObject("StubPrefab");
            _prefab = _prefabGO.AddComponent<StubPoolable>();
        }

        [TearDown]
        public void TearDown()
        {
            ObjectPool.Clear();
            if (_prefabGO != null)
                Object.Destroy(_prefabGO);
        }

        // ── Prewarm ───────────────────────────────────────────────────────────────

        [UnityTest]
        public IEnumerator Prewarm_CreatesInactiveInstances()
        {
            ObjectPool.Prewarm(_prefab, 3);
            yield return null;

            // Pool is internal; verify via Get — should not instantiate a new one.
            var a = ObjectPool.Get<StubPoolable>();
            var b = ObjectPool.Get<StubPoolable>();
            var c = ObjectPool.Get<StubPoolable>();

            Assert.NotNull(a);
            Assert.NotNull(b);
            Assert.NotNull(c);
            Assert.That(a, Is.Not.SameAs(b));
            Assert.That(b, Is.Not.SameAs(c));
        }

        // ── Get ───────────────────────────────────────────────────────────────────

        [UnityTest]
        public IEnumerator Get_ReturnsActiveInstance()
        {
            ObjectPool.Prewarm(_prefab, 1);
            yield return null;

            var instance = ObjectPool.Get<StubPoolable>();

            Assert.NotNull(instance);
            Assert.IsTrue(instance.gameObject.activeSelf);
        }

        [UnityTest]
        public IEnumerator Get_ReturnsNull_WhenPoolNotPrimed()
        {
            yield return null;

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(".*never primed.*"));
            var instance = ObjectPool.Get<StubPoolable>();

            Assert.IsNull(instance);
        }

        [UnityTest]
        public IEnumerator Get_InstantiatesExtra_WhenQueueEmpty()
        {
            ObjectPool.Prewarm(_prefab, 1);
            yield return null;

            var first  = ObjectPool.Get<StubPoolable>();
            var second = ObjectPool.Get<StubPoolable>(); // queue empty — must instantiate new

            Assert.NotNull(first);
            Assert.NotNull(second);
            Assert.That(first, Is.Not.SameAs(second));
        }

        // ── Release ───────────────────────────────────────────────────────────────

        [UnityTest]
        public IEnumerator Release_DeactivatesInstance()
        {
            ObjectPool.Prewarm(_prefab, 1);
            yield return null;

            var instance = ObjectPool.Get<StubPoolable>();
            ObjectPool.Release(instance);

            Assert.IsFalse(instance.gameObject.activeSelf);
        }

        [UnityTest]
        public IEnumerator Release_ThenGet_ReturnsSameInstance()
        {
            ObjectPool.Prewarm(_prefab, 1);
            yield return null;

            var instance = ObjectPool.Get<StubPoolable>();
            ObjectPool.Release(instance);
            var recycled = ObjectPool.Get<StubPoolable>();

            Assert.That(recycled, Is.SameAs(instance));
        }

        // ── Test double ───────────────────────────────────────────────────────────

        private class StubPoolable : MonoBehaviour { }
    }
}
