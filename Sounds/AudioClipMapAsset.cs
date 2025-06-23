using System.Collections.Generic;
using UnityEngine;

namespace minyee2913.Utils
{
    [System.Serializable]
    public struct AudioClipPair
    {
        public string key;
        public AudioClip clip;
    }
    [CreateAssetMenu(fileName = "AudioClipMap", menuName = "Audio/Clip Map Asset")]
    public class AudioClipMapAsset : ScriptableObject
    {
        public List<AudioClipPair> clipPaths = new();
    }
}
