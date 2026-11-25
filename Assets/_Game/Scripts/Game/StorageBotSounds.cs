using Stealth.Audio;
using UnityEngine;

namespace Stealth.Game
{
    /// <summary>
    /// Linus heard, on every peer, from what the server says he does (<see cref="StorageBot.Motion"/>, <see cref="StorageBot.Mode"/>,
    /// <see cref="StorageBot.Pets"/>) and how he moves: his paws on the floor and the backpack's clink as he runs, the steel of
    /// a duct giving as he crawls, the ladder's rungs, the leap up into a grate and the drop out of one, the hop into and out
    /// of the car's bed, his chatter answering a call, a squeak at an alarm, a pleased chatter when petted, a sniff while he
    /// waits sat, a snore asleep in the trunk (<see cref="Asleep"/>: the trunk's sleeping model, which only snores). Clips
    /// from recordings (Tools/Audio/recorded.py mule_*); through floors they are muffled like every sound.
    /// </summary>
    public class StorageBotSounds : MonoBehaviour
    {
        [Tooltip("Only asleep (the model curled up in the car's trunk while the real one is not in a house): just snores.")]
        public bool Asleep;

        StorageBot m_Bot;
        Vector3 m_Last;
        float m_Paws, m_Rungs, m_Duct, m_NextIdle;
        int m_Steps, m_Pets;
        BotMotion m_Motion;
        BotMode m_Mode;

        void Awake()
        {
            m_Bot = GetComponent<StorageBot>();
            m_Last = transform.position;
            if (m_Bot != null) { m_Motion = m_Bot.Motion; m_Mode = m_Bot.Mode; m_Pets = m_Bot.Pets; }
        }

        void OnEnable() => m_NextIdle = Time.time + StorageBotRules.SnoreEvery * 0.5f;

        void Update()
        {
            var bank = Sfx.Bank;
            if (bank == null) return;
            var pos = transform.position;
            var moved = pos - m_Last;
            m_Last = pos;
            if (moved.sqrMagnitude > 4f) moved = Vector3.zero; // (a warp, a teleport)
            if (Asleep || m_Bot == null)
            {
                Idle(bank.MuleSnore, StorageBotRules.SnoreEvery, StorageBotRules.SnoreEvery, 0.5f);
                return;
            }
            var motion = m_Bot.Motion;
            Play(StorageBotRules.CueOf(m_Motion, motion), bank);
            Play(StorageBotRules.CueOf(m_Mode, m_Bot.Mode), bank);
            m_Motion = motion;
            m_Mode = m_Bot.Mode;
            if (m_Bot.Pets != m_Pets)
            {
                m_Pets = m_Bot.Pets;
                Sfx.At(bank.MuleHappy, pos + Vector3.up * 0.4f, 0.8f);
            }
            float flat = new Vector2(moved.x, moved.z).magnitude;
            switch (motion)
            {
                case BotMotion.Floor:
                    for (int n = AudioRules.Rungs(ref m_Paws, flat, StorageBotRules.PawSpacing); n > 0; n--)
                    {
                        Sfx.At(SoundBank.Any(bank.MuleStep), pos, 0.55f, Sfx.Group.Footsteps);
                        if (++m_Steps % StorageBotRules.PackEvery == 0) Sfx.At(SoundBank.Any(bank.MulePack), pos + Vector3.up * 0.6f, 0.5f, Sfx.Group.Footsteps);
                    }
                    break;
                case BotMotion.Duct:
                    for (int n = AudioRules.Rungs(ref m_Duct, flat, StorageBotRules.DuctKnockSpacing); n > 0; n--)
                        Sfx.At(SoundBank.Any(bank.MuleDuct), pos, 0.6f, Sfx.Group.Footsteps);
                    break;
                case BotMotion.Ladder:
                    for (int n = AudioRules.Rungs(ref m_Rungs, moved.y); n > 0; n--)
                        Sfx.At(SoundBank.Any(bank.LadderRung), pos, 0.35f, Sfx.Group.Footsteps, 1.45f); // (a paw, not a boot: lighter, higher)
                    break;
                case BotMotion.Curled:
                    Idle(bank.MuleSnore, StorageBotRules.SnoreEvery, StorageBotRules.SnoreEvery, 0.5f);
                    break;
                case BotMotion.Sit:
                    Idle(bank.MuleSniff, StorageBotRules.SniffMin, StorageBotRules.SniffMax, 0.55f);
                    break;
            }
        }

        /// <summary>An idle sound now and then (the next one between <paramref name="min"/> and <paramref name="max"/> seconds on).</summary>
        void Idle(AudioClip clip, float min, float max, float volume)
        {
            if (Time.time < m_NextIdle) return;
            var r = Stealth.Level.LevelRandom.Shared(Stealth.Level.RandomChannel.Audio);
            m_NextIdle = Time.time + min + (float)r.NextDouble() * (max - min);
            Sfx.At(clip, transform.position + Vector3.up * 0.3f, volume);
        }

        void Play(StorageBotRules.Cue cue, SoundBank bank)
        {
            var at = transform.position + Vector3.up * 0.4f;
            switch (cue)
            {
                case StorageBotRules.Cue.Leap: Sfx.At(bank.MuleLeap, at, 0.85f); break;
                case StorageBotRules.Cue.Drop: Sfx.At(bank.MuleDrop, at, 0.85f); break;
                case StorageBotRules.Cue.Hop: Sfx.At(bank.MuleHop, at, 0.7f); break;
                case StorageBotRules.Cue.Chatter: Sfx.At(SoundBank.Any(bank.MuleChatter), at, 0.9f); break;
                case StorageBotRules.Cue.Squeak: Sfx.At(bank.MuleSqueak, at, 0.9f); break;
            }
        }
    }
}
