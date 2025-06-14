using System.Collections.Generic;
using UnityEngine;

namespace minyee2913.Utils {
    public class SoundManager : Singleton<SoundManager>
    {
        const string path = "Sounds/";
        public const int trackSize = 4;

        Dictionary<string, AudioClip> caches = new();
        [SerializeField]
        List<AudioSource> tracks = new();

        void Awake()
        {
            for (int i = 0; i < trackSize; i++)
            {
                InstantiateTrack();
            }
        }

        void InstantiateTrack()
        {
            GameObject obj = new GameObject("track" + (tracks.Count + 1).ToString());
            obj.transform.SetParent(transform);

            AudioSource source = obj.AddComponent<AudioSource>();
            tracks.Add(source);
        }

        // Update is called once per frame
        void Update()
        {

        }

        AudioClip GetClip(string sound)
        {
            if (caches.ContainsKey(sound))
            {
                return caches[sound];
            }

            AudioClip clip = Resources.Load<AudioClip>(path + sound);

            if (clip != null)
            {
                caches[sound] = clip;
            }

            return clip;
        }

        public void PlaySound(string sound, int track, float volume = 1, float pitch = 1, bool loop = false, float startTime = 0)
        {
            AudioClip clip = GetClip(sound);

            AudioSource _audio = tracks[track - 1];

            _audio.clip = clip;
            _audio.loop = loop;
            _audio.volume = volume;
            _audio.pitch = pitch;
            _audio.time = startTime;

            if (pitch != 0)
            {
                _audio.pitch = pitch;
            }

            _audio.Play();
        }

        public void StopTrack(int track)
        {
            AudioSource _audio = tracks[track - 1];

            _audio.Stop();
        }

        public void PauseTrack(int track)
        {
            AudioSource _audio = tracks[track - 1];

            _audio.Pause();
        }

        public void ResumeTrack(int track)
        {
            AudioSource _audio = tracks[track - 1];

            _audio.UnPause();
        }
    }
}
