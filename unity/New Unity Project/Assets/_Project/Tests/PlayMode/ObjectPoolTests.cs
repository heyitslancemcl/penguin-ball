using System.Collections;
using NUnit.Framework;
using PenguineBall.Core;
using UnityEngine;
using UnityEngine.TestTools;

namespace PenguineBall.Tests
{
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
            Assert.IsNull(ObjectPool.Get<StubPoolable>());
        }

        [UnityTest]
        public IEnumerator Get_InstantiatesExtra_WhenQueueEmpty()
        {
            ObjectPool.Prewarm(_prefab, 1);
            yield return null;
            var first  = ObjectPool.Get<StubPoolable>();
            var second = ObjectPool.Get<StubPoolable>();
            Assert.NotNull(first);
            Assert.NotNull(second);
            Assert.That(first, Is.Not.SameAs(second));
        }

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
            Assert.That(ObjectPool.Get<StubPoolable>(), Is.SameAs(instance));
        }

        private class StubPoolable : MonoBehaviour { }
    }
}
