using UnityEngine;

namespace Stealth.Audio
{
    /// <summary>
    /// What game code calls to make a sound: <c>Sfx.At(Sfx.Bank?.Pickup, position)</c>. Plays through the scene's
    /// <see cref="AudioDirector"/>; without one (tests, tools) every call is a no-op.
    /// </summary>
    public static class Sfx
    {
        public enum Group { Robots, Items, Footsteps, Ui }

        public static SoundBank Bank => AudioDirector.Instance != null ? AudioDirector.Instance.Bank : null;

        /// <summary>A one-shot in the world (3D).</summary>
        public static void At(AudioClip clip, Vector3 position, float volume = 1f, Group group = Group.Robots, float pitch = 1f)
        {
            if (clip != null && AudioDirector.Instance != null) AudioDirector.Instance.PlayAt(clip, position, volume, group, pitch);
        }

        /// <summary>A one-shot without a position (interface, things that happen to you).</summary>
        public static void Ui(AudioClip clip, float volume = 1f, Group group = Group.Ui)
        {
            if (clip != null && AudioDirector.Instance != null) AudioDirector.Instance.PlayFlat(clip, volume, group);
        }
    }
}
