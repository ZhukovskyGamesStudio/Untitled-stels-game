using Stealth.Audio;
using Stealth.Configs;
using Stealth.Game;
using Stealth.Items;
using Stealth.Player;
using UnityEngine;

namespace Stealth.Vehicles
{
    /// <summary>
    /// The car's radio (the unique upgrade «Магнитола», model car_radio from Tools/Blender/car_upgrades.py, shown by
    /// CarUpgradeLook): with a cassette in it (<see cref="GameManager.Cassette"/>, replicated) its song plays from the
    /// dashboard — heard round the car (a 3D source that dies away in <see cref="Range"/>), so near the car and all the drive
    /// through the tunnels; muffled through floors like every sound. A cassette is put in by dragging it onto the radio with
    /// the backpack open (InventoryUI → PlayerInventory.RequestCassette); <see cref="Slot"/> is what the drag aims at.
    /// </summary>
    public class CarRadio : MonoBehaviour
    {
        public AudioSource Source;
        [Tooltip("The radio's front (a collider): a cassette let go on it goes in.")]
        public Collider Slot;
        [Tooltip("The songs heard this far from the radio, metres (dying away linearly).")]
        public float Range = 16f;
        [Tooltip("Its loudness against the music setting.")]
        [Range(0f, 1.5f)] public float Loudness = 0.9f;

        int m_Song = -2;
        float m_NextMuffle, m_Muffle = 1f;

        /// <summary>The radio of the car now (only one car).</summary>
        public static CarRadio Active { get; private set; }

        void OnEnable() => Active = this;

        void OnDisable()
        {
            if (Active == this) Active = null;
            if (Source != null) Source.Stop();
            m_Song = -2;
        }

        void Update()
        {
            var gm = GameManager.Instance;
            var bank = Sfx.Bank;
            if (Source == null || gm == null || bank == null) return;
            int song = gm.Upgrades.Level(CarUpgrade.Radio) > 0 ? gm.Cassette : -1;
            var clips = bank.Cassettes;
            if (song != m_Song)
            {
                m_Song = song;
                Source.Stop();
                if (song >= 0 && clips != null && song < clips.Length && clips[song] != null)
                {
                    Source.clip = clips[song];
                    Source.loop = true;
                    Source.spatialBlend = 1f;
                    Source.rolloffMode = AudioRolloffMode.Linear;
                    Source.minDistance = 1.5f;
                    Source.maxDistance = Range;
                    Source.dopplerLevel = 0f;
                    Source.Play();
                }
            }
            if (!Source.isPlaying) return;
            if (Time.time >= m_NextMuffle && AudioDirector.Instance != null)
            {
                m_NextMuffle = Time.time + 0.5f;
                m_Muffle = AudioDirector.Instance.Muffle(transform.position);
            }
            Source.volume = Mathf.Clamp01(Tuning.Audio.Mix.Music * GameSettings.MusicVolume * Loudness * m_Muffle);
        }
    }
}
