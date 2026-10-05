// UnityEngine.Events - part of the Unity Converter plugin.
//
// UnityAction and UnityEvent are ordinary delegates and listener lists; there
// is nothing engine-specific about them, so these are complete. The one
// difference from Unity is persistent (Inspector-assigned) listeners: a
// Button's are bound by the UI instead (see UnityUI.cs), and any other
// event's are not imported.

using System;
using System.Collections.Generic;

namespace UnityEngine.Events
{
    public delegate void UnityAction();
    public delegate void UnityAction<T0>(T0 arg0);
    public delegate void UnityAction<T0, T1>(T0 arg0, T1 arg1);
    public delegate void UnityAction<T0, T1, T2>(T0 arg0, T1 arg1, T2 arg2);
    public delegate void UnityAction<T0, T1, T2, T3>(T0 arg0, T1 arg1, T2 arg2, T3 arg3);

    public enum PersistentListenerMode { EventDefined, Void, Object, Int, Float, String, Bool }
    public enum UnityEventCallState { Off, EditorAndRuntime, RuntimeOnly }

    [Serializable]
    public abstract class UnityEventBase
    {
        public int GetPersistentEventCount() => 0;
        public string GetPersistentMethodName(int index) => string.Empty;
        public void SetPersistentListenerState(int index, UnityEventCallState state) { }
        public abstract void RemoveAllListeners();

        // A listener that throws must not stop the rest, and must not take
        // the caller down with it - Unity logs and carries on.
        protected static void Report(Exception e)
        {
            Elintria.Debug.LogError("UnityEvent listener threw: " +
                                    (e.InnerException ?? e).GetType().Name + ": " +
                                    (e.InnerException ?? e).Message);
        }
    }

    [Serializable]
    public class UnityEvent : UnityEventBase
    {
        private readonly List<UnityAction> m_calls = new List<UnityAction>();

        public void AddListener(UnityAction call) { if (call != null) m_calls.Add(call); }
        public void RemoveListener(UnityAction call) { m_calls.Remove(call); }
        public override void RemoveAllListeners() { m_calls.Clear(); }

        public void Invoke()
        {
            UnityAction[] snapshot = m_calls.ToArray();
            for (int i = 0; i < snapshot.Length; ++i)
            {
                try { snapshot[i](); } catch (Exception e) { Report(e); }
            }
        }
    }

    [Serializable]
    public class UnityEvent<T0> : UnityEventBase
    {
        private readonly List<UnityAction<T0>> m_calls = new List<UnityAction<T0>>();

        public void AddListener(UnityAction<T0> call) { if (call != null) m_calls.Add(call); }
        public void RemoveListener(UnityAction<T0> call) { m_calls.Remove(call); }
        public override void RemoveAllListeners() { m_calls.Clear(); }

        public void Invoke(T0 arg0)
        {
            UnityAction<T0>[] snapshot = m_calls.ToArray();
            for (int i = 0; i < snapshot.Length; ++i)
            {
                try { snapshot[i](arg0); } catch (Exception e) { Report(e); }
            }
        }
    }

    [Serializable]
    public class UnityEvent<T0, T1> : UnityEventBase
    {
        private readonly List<UnityAction<T0, T1>> m_calls = new List<UnityAction<T0, T1>>();

        public void AddListener(UnityAction<T0, T1> call) { if (call != null) m_calls.Add(call); }
        public void RemoveListener(UnityAction<T0, T1> call) { m_calls.Remove(call); }
        public override void RemoveAllListeners() { m_calls.Clear(); }

        public void Invoke(T0 arg0, T1 arg1)
        {
            UnityAction<T0, T1>[] snapshot = m_calls.ToArray();
            for (int i = 0; i < snapshot.Length; ++i)
            {
                try { snapshot[i](arg0, arg1); } catch (Exception e) { Report(e); }
            }
        }
    }

    [Serializable]
    public class UnityEvent<T0, T1, T2> : UnityEventBase
    {
        private readonly List<UnityAction<T0, T1, T2>> m_calls = new List<UnityAction<T0, T1, T2>>();

        public void AddListener(UnityAction<T0, T1, T2> call) { if (call != null) m_calls.Add(call); }
        public void RemoveListener(UnityAction<T0, T1, T2> call) { m_calls.Remove(call); }
        public override void RemoveAllListeners() { m_calls.Clear(); }

        public void Invoke(T0 arg0, T1 arg1, T2 arg2)
        {
            UnityAction<T0, T1, T2>[] snapshot = m_calls.ToArray();
            for (int i = 0; i < snapshot.Length; ++i)
            {
                try { snapshot[i](arg0, arg1, arg2); } catch (Exception e) { Report(e); }
            }
        }
    }
}
