using System;
using System.Collections.Generic;
using UnityEngine;

namespace PenguineBall.Core
{
    /// <summary>
    /// ScriptableObject event channel. Decouples event producers from consumers.
    /// All SO events in the project (OnLevelCompleted, OnFireAbilityActivated, etc.)
    /// use this same type. ADR-009: Hybrid SO Events + Service Locator.
    /// </summary>
    [CreateAssetMenu(menuName = "PenguineBall/GameEvent")]
    public class GameEventSO : ScriptableObject
    {
        private readonly List<Action> _listeners = new();

        public void AddListener(Action listener) => _listeners.Add(listener);
        public void RemoveListener(Action listener) => _listeners.Remove(listener);

        public void Raise()
        {
            // Iterate a copy — listeners may unsubscribe during Raise
            for (int i = _listeners.Count - 1; i >= 0; i--)
                _listeners[i]?.Invoke();
        }
    }
}
