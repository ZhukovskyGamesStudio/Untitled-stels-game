using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Stealth.UI
{
    /// <summary>
    /// Grows a set of element instances from a pre-built prefab on demand and hides the unused ones.
    /// Usage per frame: <see cref="Begin"/>, <see cref="Next"/> for every visible element, <see cref="End"/>.
    /// </summary>
    public sealed class UiPool<T> where T : Component
    {
        readonly T m_Prefab;
        readonly Transform m_Parent;
        readonly List<T> m_Items = new();
        int m_Used;

        public UiPool(T prefab, Transform parent)
        {
            m_Prefab = prefab;
            m_Parent = parent;
        }

        public int Count => m_Items.Count;
        public int Used => m_Used;

        public void Begin() => m_Used = 0;

        public T Next()
        {
            if (m_Used == m_Items.Count) m_Items.Add(Object.Instantiate(m_Prefab, m_Parent, false));
            var item = m_Items[m_Used++];
            if (!item.gameObject.activeSelf) item.gameObject.SetActive(true);
            return item;
        }

        public void End()
        {
            for (int i = m_Used; i < m_Items.Count; i++)
                if (m_Items[i].gameObject.activeSelf) m_Items[i].gameObject.SetActive(false);
        }
    }

    /// <summary>Small helpers to drive pre-built UI elements.</summary>
    public static class Ui
    {
        public static void Show(Component c, bool visible)
        {
            if (c != null && c.gameObject.activeSelf != visible) c.gameObject.SetActive(visible);
        }

        public static void Show(GameObject go, bool visible)
        {
            if (go != null && go.activeSelf != visible) go.SetActive(visible);
        }

        public static void Alpha(Graphic g, float a)
        {
            var c = g.color;
            if (!Mathf.Approximately(c.a, a)) g.color = new Color(c.r, c.g, c.b, a);
        }

        public static void Set(Text t, string text, Color color)
        {
            if (t.text != text) t.text = text;
            if (t.color != color) t.color = color;
        }

        /// <summary>Canvas point (bottom-left origin, canvas units) of a viewport point.</summary>
        public static Vector2 FromViewport(Vector3 viewport, Vector2 canvasSize) => new(viewport.x * canvasSize.x, viewport.y * canvasSize.y);
    }
}
