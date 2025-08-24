using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
using System.IO;
#endif
using UnityEngine;

namespace minyee2913.Utils {
    public class SoundManager : Singleton<SoundManager>
    {
        public AudioClipMapAsset preloadAsset;
        const string path = "Sounds/";
        public int trackSize = 4;

        Dictionary<string, AudioClip> caches = new();
        public IReadOnlyDictionary<string, AudioClip> AudioClipMap => caches;
        [SerializeField]
        List<AudioSource> tracks = new();

        void Awake()
        {
            for (int i = 0; i < trackSize; i++)
            {
                InstantiateTrack();
            }

			if (preloadAsset != null)
			{
				foreach (var pair in preloadAsset.clipPaths)
				{
					caches[pair.key] = pair.clip;
				}
			}

			foreach (var clip in caches.Values)
			{
				if (clip != null && clip.loadState != AudioDataLoadState.Loaded)
				{
					clip.LoadAudioData();
				}
			}
        }

		public AudioSource GetTrack(int index)
		{
			return tracks[index-1];
		}

        void InstantiateTrack()
        {
            GameObject obj = new GameObject("track" + (tracks.Count + 1).ToString());
            obj.transform.SetParent(transform);

            AudioSource source = obj.AddComponent<AudioSource>();
			source.playOnAwake = false;
            tracks.Add(source);
        }

        // Update is called once per frame
        void Update()
        {

        }

        public void PreCache(string sound)
        {
            if (caches.ContainsKey(sound))
            {
                return;
            }

            AudioClip clip = Resources.Load<AudioClip>(path + sound);

            if (clip != null)
            {
				caches[sound] = clip;
				if (clip.loadState != AudioDataLoadState.Loaded)
				{
					clip.LoadAudioData();
				}
            }
        }

        #if UNITY_EDITOR
        public void PreloadAllClips()
        {
            // 에디터에서 모든 AudioClip 로드
            //string resourcesPrefix = "Assets/Resources/";
            string outputPath = "Assets/Resources/Sounds/AudioClipMap.asset";

            // var clips = Resources.LoadAll<AudioClip>("Sounds");
            var clipPaths = new List<AudioClipPair>();

            // foreach (var clip in clips)
            // {
            //     string assetPath = AssetDatabase.GetAssetPath(clip);
            //     if (!assetPath.StartsWith(resourcesPrefix)) continue;

            //     string relativePath = assetPath.Replace(resourcesPrefix, "");
            //     relativePath = Path.ChangeExtension(relativePath, null); // .wav 제거
            //     clipPaths.Add(new AudioClipPair{key = relativePath, clip = });
            // }

            var clips = Resources.LoadAll<AudioClip>("Sounds");
            foreach (var clip in clips)
            {
                string path = AssetDatabase.GetAssetPath(clip);
                if (!path.StartsWith("Assets/Resources/")) continue;

                string relativePath = path.Replace("Assets/Resources/", "");
                relativePath = Path.ChangeExtension(relativePath, null).Replace("Sounds/", ""); // remove .wav

                if (clipPaths.Find((v)=>v.key == relativePath).clip == null)
                    clipPaths.Add(new AudioClipPair{key = relativePath, clip = clip});
            }

            // ScriptableObject 생성 또는 불러오기
            preloadAsset = AssetDatabase.LoadAssetAtPath<AudioClipMapAsset>(outputPath);
            if (preloadAsset == null)
            {
                preloadAsset = ScriptableObject.CreateInstance<AudioClipMapAsset>();
                Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
                AssetDatabase.CreateAsset(preloadAsset, outputPath);
                Debug.Log("📄 AudioClipMap.asset 생성됨");
            }

            preloadAsset.clipPaths = clipPaths;
            EditorUtility.SetDirty(preloadAsset);
            AssetDatabase.SaveAssets();

            EditorUtility.SetDirty(this);
        }

        public void ClearClipCache()
        {
            caches.Clear();
            EditorUtility.SetDirty(this);

            if (preloadAsset != null)
            {
                preloadAsset.clipPaths.Clear();
            }
        }
        #endif

        AudioClip GetClip(string sound)
        {
			if (caches.ContainsKey(sound))
			{
				var cachedClip = caches[sound];
				if (cachedClip != null && cachedClip.loadState != AudioDataLoadState.Loaded)
				{
					cachedClip.LoadAudioData();
				}
				return cachedClip;
			}

            AudioClip clip = Resources.Load<AudioClip>(path + sound);

            if (clip != null)
            {
				caches[sound] = clip;
				if (clip.loadState != AudioDataLoadState.Loaded)
				{
					clip.LoadAudioData();
				}
            }

            return clip;
        }

		bool IsValidTrack(int track)
		{
			return track >= 1 && track <= tracks.Count;
		}

		public void PlaySound(string sound, int track, float volume = 1, float pitch = 1, bool loop = false, float startTime = 0)
		{
			if (!IsValidTrack(track))
			{
				Debug.LogWarning($"[SoundManager] Invalid track index: {track}");
				return;
			}

			AudioClip clip = GetClip(sound);
			if (clip == null)
			{
				Debug.LogWarning($"[SoundManager] AudioClip not found for key: {sound}");
				return;
			}

			AudioSource _audio = tracks[track - 1];

			_audio.clip = clip;
			_audio.loop = loop;
			_audio.volume = volume;
			_audio.pitch = pitch;
			if (startTime > 0f)
			{
				float clampedStart = Mathf.Clamp(startTime, 0f, clip.length > 0f ? clip.length - 0.01f : 0f);
				_audio.time = clampedStart;
			}

			_audio.Play();
		}

		public void StopTrack(int track)
		{
			if (!IsValidTrack(track)) return;
			AudioSource _audio = tracks[track - 1];
			_audio.Stop();
		}

		public void PauseTrack(int track)
		{
			if (!IsValidTrack(track)) return;
			AudioSource _audio = tracks[track - 1];
			_audio.Pause();
		}

		public void ResumeTrack(int track)
		{
			if (!IsValidTrack(track)) return;
			AudioSource _audio = tracks[track - 1];
			_audio.UnPause();
		}

		public void PlaySoundWithFade(string sound, int track, float volume = 1, float pitch = 1, bool loop = false, float startTime = 0, float fadeTime = 1f)
		{
			if (!IsValidTrack(track))
			{
				Debug.LogWarning($"[SoundManager] Invalid track index: {track}");
				return;
			}

			AudioClip clip = GetClip(sound);
			if (clip == null)
			{
				Debug.LogWarning($"[SoundManager] AudioClip not found for key: {sound}");
				return;
			}

			AudioSource _audio = tracks[track - 1];
			
			// 기존 사운드가 재생 중이면 페이드 아웃 후 새 사운드 재생
			if (_audio.isPlaying)
			{
				StartCoroutine(FadeOutThenPlay(track, sound, volume, pitch, loop, startTime, fadeTime));
			}
			else
			{
				// 기존 사운드가 없으면 바로 페이드 인으로 재생
				StartCoroutine(FadeInTrack(track, sound, volume, pitch, loop, startTime, fadeTime));
			}
		}

		private System.Collections.IEnumerator FadeOutThenPlay(int track, string sound, float volume, float pitch, bool loop, float startTime, float fadeTime)
		{
			if (!IsValidTrack(track)) yield break;
			
			AudioSource _audio = tracks[track - 1];
			float startVolume = _audio.volume;
			float elapsedTime = 0f;

			// 페이드 아웃
			while (elapsedTime < fadeTime)
			{
				elapsedTime += Time.deltaTime;
				_audio.volume = Mathf.Lerp(startVolume, 0f, elapsedTime / fadeTime);
				yield return null;
			}

			_audio.Stop();
			_audio.volume = startVolume; // 원래 볼륨으로 복원

			// 페이드 아웃 완료 후 새 사운드 재생
			yield return StartCoroutine(FadeInTrack(track, sound, volume, pitch, loop, startTime, fadeTime));
		}

		private System.Collections.IEnumerator FadeInTrack(int track, string sound, float volume, float pitch, bool loop, float startTime, float fadeTime)
		{
			if (!IsValidTrack(track)) yield break;
			
			AudioSource _audio = tracks[track - 1];
			
			// 새 사운드 설정
			AudioClip clip = GetClip(sound);
			if (clip == null) yield break;
			
			_audio.clip = clip;
			_audio.loop = loop;
			_audio.pitch = pitch;
			if (startTime > 0f)
			{
				float clampedStart = Mathf.Clamp(startTime, 0f, clip.length > 0f ? clip.length - 0.01f : 0f);
				_audio.time = clampedStart;
			}

			_audio.volume = 0f;
			_audio.Play();

			// 페이드 인
			float elapsedTime = 0f;
			while (elapsedTime < fadeTime)
			{
				elapsedTime += Time.deltaTime;
				_audio.volume = Mathf.Lerp(0f, volume, elapsedTime / fadeTime);
				yield return null;
			}

			_audio.volume = volume; // 정확한 타겟 볼륨으로 설정
		}
    }
}
