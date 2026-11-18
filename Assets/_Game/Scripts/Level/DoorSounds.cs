using UnityEngine;

namespace Stealth.Level
{
    /// <summary>The sound of something that opens or shuts (a door, a lift's doors, the shaft's lid): one place, one loudness (<see cref="Configs.AudioMix.Doors"/>).</summary>
    public static class DoorSounds
    {
        public static void Play(DoorSound sound, Vector3 at, bool open)
        {
            var bank = Audio.Sfx.Bank;
            float loud = Configs.Tuning.Audio.Mix.Doors;
            if (sound == DoorSound.Slide || bank == null || bank.DoorOpen == null || bank.DoorShut == null)
                Audio.Sfx.At(bank?.DoorSlide, at, loud, Audio.Sfx.Group.Items, open ? 1f : 0.92f);
            else
                Audio.Sfx.At(open ? bank.DoorOpen : bank.DoorShut, at, sound == DoorSound.Heavy ? loud * 1.3f : loud, Audio.Sfx.Group.Items,
                    sound == DoorSound.Heavy ? 0.72f : 1f);
        }
    }
}
