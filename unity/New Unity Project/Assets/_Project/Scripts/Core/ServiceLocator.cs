using System;
using System.Collections.Generic;

namespace PenguineBall.Core
{
    /// <summary>
    /// Static service locator for system-to-system dependencies.
    /// Register in Bootstrap.Awake(); retrieve in any Start().
    /// ADR-009: Code Architecture Pattern.
    /// </summary>
    public static class ServiceLocator
    {
        private static readonly Dictionary<Type, object> _services = new();

        /// <summary>
        /// Register a service implementation. Uses dictionary assignment (not .Add)
        /// so later registrations can overwrite earlier ones (e.g. Story 6.3 replaces
        /// NullAnalyticsService with GameAnalyticsService).
        /// </summary>
        public static void Register<T>(T instance)
        {
            _services[typeof(T)] = instance;
        }

        /// <summary>
        /// Retrieve a registered service.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown when no service is registered for the requested type.
        /// </exception>
        public static T Get<T>()
        {
            if (_services.TryGetValue(typeof(T), out var service))
                return (T)service;

            throw new InvalidOperationException(
                $"ServiceLocator: No service registered for type {typeof(T).Name}");
        }

        /// <summary>
        /// Remove all registered services. For test teardown only — do NOT call in production.
        /// </summary>
        public static void Clear()
        {
            _services.Clear();
        }
    }
}
