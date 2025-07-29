using System;
using System.Runtime.CompilerServices;
using System.Collections.Generic;

namespace EyeRest.Infrastructure
{
    public static class WeakEventManager
    {
        private static readonly ConditionalWeakTable<object, List<WeakEventSubscription>> _subscriptions = new();

        public static void Subscribe<T>(object source, string eventName, EventHandler<T> handler) where T : EventArgs
        {
            var subscriptions = _subscriptions.GetOrCreateValue(source);
            subscriptions.Add(new WeakEventSubscription(eventName, handler.Target, handler.Method));
        }

        public static void Unsubscribe<T>(object source, string eventName, EventHandler<T> handler) where T : EventArgs
        {
            if (_subscriptions.TryGetValue(source, out var subscriptions))
            {
                subscriptions.RemoveAll(s => s.EventName == eventName && 
                                           s.TargetReference.Target == handler.Target && 
                                           s.Method == handler.Method);
            }
        }

        public static void RaiseEvent<T>(object source, string eventName, T eventArgs) where T : EventArgs
        {
            if (_subscriptions.TryGetValue(source, out var subscriptions))
            {
                var toRemove = new List<WeakEventSubscription>();
                
                foreach (var subscription in subscriptions)
                {
                    if (subscription.EventName == eventName)
                    {
                        var target = subscription.TargetReference.Target;
                        if (target != null)
                        {
                            try
                            {
                                subscription.Method.Invoke(target, new object[] { source, eventArgs });
                            }
                            catch
                            {
                                // Ignore errors in event handlers
                            }
                        }
                        else
                        {
                            // Target has been garbage collected
                            toRemove.Add(subscription);
                        }
                    }
                }

                // Clean up dead references
                foreach (var deadSubscription in toRemove)
                {
                    subscriptions.Remove(deadSubscription);
                }
            }
        }

        private class WeakEventSubscription
        {
            public string EventName { get; }
            public WeakReference TargetReference { get; }
            public System.Reflection.MethodInfo Method { get; }

            public WeakEventSubscription(string eventName, object? target, System.Reflection.MethodInfo method)
            {
                EventName = eventName;
                TargetReference = new WeakReference(target);
                Method = method;
            }
        }
    }
}