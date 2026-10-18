using System.Collections.Generic;
using UnityEngine;

namespace MiCay
{
    public static class Sfx
    {
        const string PrefKey = "micay_sfx_on";
        const string MusicKey = "micay_music_on";
        const float MusicVolume = 0.32f;
        const float Volume = 0.8f;
        const float MinRepeat = 0.06f;

        static AudioSource source, music;
        static string musicId;
        static readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        static readonly Dictionary<string, float> lastPlayed = new Dictionary<string, float>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            source = null;
            music = null;
            musicId = null;
            clips.Clear();
            lastPlayed.Clear();
        }

        public static bool Enabled
        {
            get => PlayerPrefs.GetInt(PrefKey, 1) == 1;
            set
            {
                PlayerPrefs.SetInt(PrefKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        public static bool MusicEnabled
        {
            get => PlayerPrefs.GetInt(MusicKey, 1) == 1;
            set
            {
                PlayerPrefs.SetInt(MusicKey, value ? 1 : 0);
                PlayerPrefs.Save();
                if (value) PlayMusic(musicId ?? "bgm_main");
                else if (music != null) music.Stop();
            }
        }

        public static void PlayMusic(string id)
        {
            musicId = id;
            if (music == null || !MusicEnabled) return;
            var clip = Load(id);
            if (clip == null) return;
            if (music.clip == clip && music.isPlaying) return;
            music.clip = clip;
            music.loop = true;
            music.volume = MusicVolume;
            music.Play();
        }

        static AudioClip Load(string id)
        {
            if (!clips.TryGetValue(id, out var clip))
            {
                clip = Resources.Load<AudioClip>("Audio/" + id);
                clips[id] = clip;
            }
            return clip;
        }

        public static void Init(GameObject host)
        {
            source = host.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            music = host.AddComponent<AudioSource>();
            music.playOnAwake = false;
            music.spatialBlend = 0f;
            if (Object.FindAnyObjectByType<AudioListener>() == null) host.AddComponent<AudioListener>();
        }

        public static void Play(string id, float volume = 1f)
        {
            if (source == null || !Enabled) return;
            if (lastPlayed.TryGetValue(id, out var last) && Time.unscaledTime - last < MinRepeat) return;
            var clip = Load(id);
            if (clip == null) return;
            lastPlayed[id] = Time.unscaledTime;
            source.PlayOneShot(clip, Volume * volume);
        }
    }
}
