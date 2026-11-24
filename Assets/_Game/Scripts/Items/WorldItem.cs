using System.Collections.Generic;
using UnityEngine;

namespace Stealth.Items
{
    /// <summary>
    /// An item lying in the world. Picked up by dragging it into the inventory grid.
    /// Its look (<see cref="Shape"/>, <see cref="Color"/>) is set by the server before spawning and replicated by the
    /// network twin (WorldItemNet) through <see cref="SetLook"/>.
    /// </summary>
    public class WorldItem : MonoBehaviour
    {
        public static readonly List<WorldItem> All = new();

        public ItemVisuals Visuals;
        public MeshFilter MeshFilter;
        public MeshRenderer Renderer;
        public BoxCollider Collider;
        [Tooltip("Physics of real items (ItemCatalog.HasPhysics): dynamic on the server, kinematic elsewhere (NetworkTransform moves it).")]
        public Rigidbody Body;
        [Tooltip("The solid box a real item lies and tumbles on (the grab box above is a larger trigger).")]
        public BoxCollider Solid;
        [Tooltip("The lure speaker's music (plays while it is on).")]
        public AudioSource Music;

        /// <summary>A lure speaker plays from / until these server times (set by the server, replicated).</summary>
        public double PlayFrom { get; private set; }
        public double PlayUntil { get; private set; }
        public bool IsPlaying => Shape == ItemCatalog.Speaker && Game.Session.ServerTime >= PlayFrom && Game.Session.ServerTime < PlayUntil;

        /// <summary>Server: the speaker starts playing at <paramref name="from"/> for <paramref name="seconds"/>.</summary>
        public void PlayServer(double from, float seconds)
        {
            PlayFrom = from;
            PlayUntil = from + seconds;
        }

        /// <summary>Server: a robot switched it off.</summary>
        public void StopPlayingServer() => PlayUntil = PlayFrom = 0;

        public void ApplyPlaying(double from, double until)
        {
            PlayFrom = from;
            PlayUntil = until;
        }

        GameObject m_Special;
        Renderer[] m_Glow;
        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        public byte Shape { get; private set; }
        public byte Color { get; private set; }

        public int Value => ItemCatalog.Value(Shape, Color);

        MaterialPropertyBlock m_Block;
        float m_HiddenUntil = -1f;
        Storage m_RidesIn;          // server: the drawer it comes out in, until that has slid all the way
        int m_RidesPart;
        Vector3 m_RideEnd;
        float m_RideUntil;

        /// <summary>Hidden locally while a pickup request is on its way (see <see cref="HideWhileTaking"/>).</summary>
        public bool Taking => m_HiddenUntil >= 0f;

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        public void SetLook(byte shape, byte color)
        {
            Shape = shape;
            Color = color;
            Apply();
        }

        /// <summary>
        /// Local: vanish right away after asking the server to take it, so a click feels instant even with network lag.
        /// The server despawns it; if it refused (no room, too far), the item shows up again after <paramref name="seconds"/>.
        /// </summary>
        public void HideWhileTaking(float now, float seconds = 1f)
        {
            m_HiddenUntil = now + seconds;
            if (Renderer != null) Renderer.enabled = false;
            if (Collider != null) Collider.enabled = false;
            if (m_Special != null) m_Special.SetActive(false);
            ApplyPhysics(hidden: true);
        }

        /// <summary>
        /// A real item falls on the server (it lies where it landed, the NetworkTransform shows it on the clients); hidden
        /// (being taken) it freezes. Tetris pieces have no solid box and stay where they were put.
        /// </summary>
        void ApplyPhysics(bool hidden)
        {
            bool physics = ItemCatalog.HasPhysics(Shape);
            if (Solid != null) Solid.enabled = physics && !hidden;
            if (Body == null) return;
            bool dynamic = physics && !hidden && m_RidesIn == null && Game.Session.IsServer;
            if (Body.isKinematic == !dynamic) return;
            Body.isKinematic = !dynamic;
            if (dynamic) Body.WakeUp();
        }

        /// <summary>
        /// Server: the item comes out with its drawer — it lies where the drawer carries it and starts to fall only once the
        /// drawer has stopped. Let go at the spot the drawer would slide to, it fell past the floor of a drawer that was not
        /// there yet and ended up under its board.
        /// </summary>
        public void RideServer(Storage drawers, int part)
        {
            if (drawers == null || !drawers.Drawers) return;
            m_RidesIn = drawers;
            m_RidesPart = part;
            m_RideEnd = transform.position;
            m_RideUntil = Time.time + StorageMath.OpenSeconds * 3f;
            Ride();
            ApplyPhysics(hidden: Taking);
        }

        void Ride()
        {
            if (m_RidesIn == null) return;
            float left = m_RidesIn.Travel - m_RidesIn.OutBy(m_RidesPart);
            transform.position = m_RideEnd - m_RidesIn.transform.forward * Mathf.Max(0f, left);
            if (left > 0.002f && Time.time < m_RideUntil) return;
            m_RidesIn = null;
            transform.position = m_RideEnd;
            ApplyPhysics(hidden: Taking);
        }

        void Update()
        {
            UpdateMusic();
            Ride();
            if (m_HiddenUntil < 0f || Time.time < m_HiddenUntil) return;
            m_HiddenUntil = -1f;
            if (Renderer != null) Renderer.enabled = true;
            if (Collider != null) Collider.enabled = true;
            if (m_Special != null) m_Special.SetActive(true);
            ApplyPhysics(hidden: false);
        }

        bool m_Playing;

        /// <summary>The speaker's music and its blinking LED, on every peer from the replicated times.</summary>
        void UpdateMusic()
        {
            bool playing = IsPlaying;
            if (playing != m_Playing)
            {
                m_Playing = playing;
                if (Music != null)
                {
                    if (playing)
                    {
                        Music.clip = Audio.Sfx.Bank != null ? Audio.Sfx.Bank.SpeakerMusic : Music.clip;
                        Music.volume = 0.9f;
                        if (Music.clip != null) Music.Play();
                    }
                    else Music.Stop();
                }
            }
            if (Music != null && playing && Audio.AudioDirector.Instance != null)
                Music.volume = 0.9f * Audio.AudioDirector.Instance.Muffle(transform.position);
            if (m_Glow == null) return;
            m_Block ??= new MaterialPropertyBlock();
            float pulse = playing ? 1.5f + Mathf.Sin(Time.time * 12f) * 1.2f : 0.15f;
            foreach (var r in m_Glow)
            {
                r.GetPropertyBlock(m_Block);
                m_Block.SetColor(EmissionId, new UnityEngine.Color(0.3f, 1f, 0.6f) * pulse);
                r.SetPropertyBlock(m_Block);
            }
        }

        /// <summary>Local-only highlight while the item is under the crosshair / cursor.</summary>
        public void SetHighlighted(bool on)
        {
            if (Renderer == null || Visuals == null) return;
            if (m_Special != null && ItemCatalog.HasPhysics(Shape))
            {
                // a real item's own model: a soft glow of its value colour while under the crosshair
                m_Block ??= new MaterialPropertyBlock();
                m_Block.Clear();
                if (on) m_Block.SetColor(EmissionId, ItemCatalog.UiColor(Shape, Color) * 0.6f);
                foreach (var r in m_Special.GetComponentsInChildren<Renderer>())
                {
                    if (!on) { r.SetPropertyBlock(null); continue; }
                    r.material.EnableKeyword("_EMISSION");
                    r.SetPropertyBlock(m_Block);
                }
                return;
            }
            if (!on) { Renderer.SetPropertyBlock(null); return; }
            m_Block ??= new MaterialPropertyBlock();
            var c = ItemCatalog.UiColor(Shape, Color);
            m_Block.SetColor("_EmissionColor", c * 1.5f); // brighter, but keeps its colour (= value)
            Renderer.SetPropertyBlock(m_Block);
        }

        void Apply()
        {
            if (Visuals == null) return;
            var mesh = Visuals.Meshes[Mathf.Clamp(Shape, 0, Visuals.Meshes.Length - 1)];
            MeshFilter.sharedMesh = mesh;
            Renderer.sharedMaterial = Visuals.ColorMaterials[Mathf.Clamp(Color, 0, Visuals.ColorMaterials.Length - 1)];
            if (mesh != null)
            {
                // a real item is sized by its own model (lying along X, centred, on the floor), a piece by its mesh
                var b = mesh.bounds;
                if (ItemCatalog.HasPhysics(Shape))
                {
                    var size = ItemCatalog.ModelSize(Shape);
                    b = new Bounds(new Vector3(0f, size.y / 2f, 0f), size);
                }
                Collider.center = b.center;
                Collider.size = b.size + Vector3.one * 0.12f; // a bit larger: easier to grab with the mouse
                if (Solid != null)
                {
                    Solid.center = b.center;
                    Solid.size = Vector3.Max(b.size, Vector3.one * 0.04f); // not paper-thin: it must not fall through the floor
                }
            }
            // a tool with a model of its own: the model instead of the tinted piece
            var model = Visuals.Models != null && Shape < Visuals.Models.Length ? Visuals.Models[Shape] : null;
            if (m_Special != null && (model == null || m_Special.name != model.name)) { Destroy(m_Special); m_Special = null; m_Glow = null; }
            if (model != null && m_Special == null)
            {
                m_Special = Instantiate(model, transform);
                m_Special.name = model.name;
                var list = new List<Renderer>();
                foreach (var r in m_Special.GetComponentsInChildren<Renderer>()) if (r.name.StartsWith("Eye")) list.Add(r);
                m_Glow = list.ToArray();
            }
            Renderer.forceRenderingOff = m_Special != null;
            ApplyPhysics(hidden: Taking);
            Level.VisualStyles.Dress(gameObject); // the item's model in the current visual style
        }
    }
}
