// The Unity components an imported object had - part of the Unity Converter
// plugin.
//
// The importer writes each object's Unity component types into its "u"
// metadata ("Image,Button,CanvasRenderer"). When a script asks the object for
// one of them - GetComponent<Image>(), or a field of that type filled from
// the scene - the engine asks this provider, which makes the matching class
// from this plugin. After that the object keeps it, so the same instance is
// found every time.

using System;
using System.Collections.Generic;
using Elintria;

namespace UnityEngine
{
    internal sealed class UnityComponentProvider : IComponentProvider
    {
        private static Dictionary<string, Type> s_byName;

        // Every component class this plugin declares, by its short name -
        // the name the importer wrote.
        private static Dictionary<string, Type> Types()
        {
            if (s_byName != null) return s_byName;
            s_byName = new Dictionary<string, Type>(StringComparer.Ordinal);
            Type[] all;
            try { all = typeof(UnityComponentProvider).Assembly.GetTypes(); }
            catch (System.Reflection.ReflectionTypeLoadException e) { all = e.Types; }
            foreach (Type t in all)
            {
                if (t == null || t.IsAbstract || !typeof(ManagedComponent).IsAssignableFrom(t)) continue;
                string ns = t.Namespace ?? "";
                if (!(ns == "UnityEngine" || ns.StartsWith("UnityEngine.", StringComparison.Ordinal) || ns == "TMPro")) continue;
                if (!s_byName.ContainsKey(t.Name)) s_byName[t.Name] = t;
            }
            return s_byName;
        }

        public ManagedComponent Provide(Elintria.GameObject go, Type wanted)
        {
            // An agent is Elintria's own component; the Unity-shaped wrapper
            // is there whenever the agent is.
            if (wanted.IsAssignableFrom(typeof(AI.NavMeshAgent)) && wanted != typeof(object) &&
                Elintria.NavAgent.Of(go).exists)
                return new AI.NavMeshAgent();

            string list = go.GetMeta("u");
            if (list.Length > 0)
            {
                Dictionary<string, Type> types = Types();
                foreach (string raw in list.Split(','))
                {
                    Type t;
                    if (!types.TryGetValue(raw.Trim(), out t)) continue;
                    if (!wanted.IsAssignableFrom(t)) continue;
                    try { return (ManagedComponent)Activator.CreateInstance(t); }
                    catch (Exception e)
                    {
                        Elintria.Debug.LogError("[Unity compat] could not make " + t.Name + ": " + e.Message);
                        return null;
                    }
                }
            }

            // Every UI object has a RectTransform in Unity. An object bound to
            // a UI element is one; so is one on the UI layer or carrying a UI
            // component - the objects of a UI prefab, placed while the game
            // runs, have no element to be bound to. (Scenes imported since
            // RectTransform is in the "u" list are answered above.)
            if (wanted == typeof(RectTransform) && IsUiObject(go, list)) return new RectTransform();
            return null;
        }

        private static bool IsUiObject(Elintria.GameObject go, string list)
        {
            if (go.GetMeta("ui").Length > 0) return true;
            if (go.GetMeta("layer") == "5") return true;
            foreach (string raw in list.Split(','))
            {
                string t = raw.Trim();
                if (t == "CanvasRenderer" || t == "Canvas" || t == "CanvasGroup" || t == "RectTransform") return true;
            }
            return false;
        }
    }
}
