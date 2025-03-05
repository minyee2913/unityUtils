using UnityEngine;

namespace minyee2913.Utils {
    public class SoundManager : Singleton<SoundManager>
    {
        const string path = "Sounds/";
        public const int trackSize = 4;
        void Start()
        {
            
        }

        // Update is called once per frame
        void Update()
        {
            
        }

        public void PlaySound(string sound, int track, float volume = 1, float pith = 1) {}
    }
}
