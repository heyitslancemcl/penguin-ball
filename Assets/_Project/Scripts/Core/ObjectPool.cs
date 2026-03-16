using System;
using System.Collections.Generic;
using UnityEngine;

namespace PenguineBall.Core
{
    /// <summary>
    /// Static generic object pool.
    /// Prewarm once in BootstrapManager.Awake(); use Get/Release during gameplay.
    /// Instantiate/Destroy are only allowed inside this class — NEVER in gameplay code.
    /// ADR-009: Code Architecture Pattern.
    /// </summary>
    public static class ObjectPool
    {
        private class PoolEntry
        {
            public Queue<MonoBehaviour> Queue = new();
            public MonoBehaviour Prefab;
        }

        private static readonly Dictionary<Type, PoolEntry> _pools = new();

        /// <summary>
        /// Pre-instantiate <paramref name="count"/> inactive instances and store the prefab
        /// for future expansion. Must be called before Get.
        /// Called in BootstrapManager.Awake() when each pooled type is introduced.
        /// </summary>
        public static void Prewarm<T>(T prefab, int count) where T : MonoBehaviour
        {
            var type = typeof(T);
            if (!_pools.TryGetValue(type, out var entry))
            {
                entry = new PoolEntry { Prefab = prefab };
                _pools[type] = entry;
            }
            else
            {
                entry.Prefab = prefab;
            }

            for (int i = 0; i < count; i++)
            {
                var instance = UnityEngine.Object.Instantiate(prefab);
                instance.gameObject.SetActive(false);
                entry.Queue.Enqueue(instance);
            }
        }

        /// <summary>
        /// Retrieve a pooled instance. Instantiates a new one from the stored prefab when
        /// the queue is empty. Returns null if the pool was never primed with Prewarm.
        /// </summary>
        public static T Get<T>() where T : MonoBehaviour
        {
            var type = typeof(T);
            if (!_pools.TryGetValue(type, out var entry) || entry.Prefab == null)
            {
                Debug.LogWarning($"ObjectPool: Pool for {type.Name} was never primed. Call Prewarm first.");
                return null;
            }

            T instance;
            if (entry.Queue.Count > 0)
            {
                instance = (T)entry.Queue.Dequeue();
            }
            else
            {
                instance = UnityEngine.Object.Instantiate((T)entry.Prefab);
            }

            instance.gameObject.SetActive(true);
            return instance;
        }

        /// <summary>
        /// Return an instance to the pool. Deactivates the object and re-enqueues it.
        /// </summary>
        public static void Release<T>(T instance) where T : MonoBehaviour
        {
            if (instance == null) return;

            instance.gameObject.SetActive(false);
            var type = typeof(T);
            if (!_pools.TryGetValue(type, out var entry))
            {
                entry = new PoolEntry();
                _pools[type] = entry;
            }
            entry.Queue.Enqueue(instance);
        }

        /// <summary>
        /// Clear all pools. For test teardown only.
        /// </summary>
        public static void Clear()
        {
            _pools.Clear();
        }
    }
}
