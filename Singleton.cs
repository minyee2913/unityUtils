using UnityEngine;

namespace minyee2913.Utils {
    public class Singleton<T> : MonoBehaviour where T : MonoBehaviour {
        private static T instance;

        virtual protected bool UseDontDestroyOnLoad => true;

        public static T Instance
        {
            get {
                if (instance == null) {
                    instance = (T)FindFirstObjectByType(typeof(T));
                }

                if (instance == null) {
                    GameObject obj = new GameObject(typeof(T).Name, typeof(T));
                    instance = obj.GetComponent<T>();
                }

                return instance;
            }
        }

        void Awake()
        {
            if (UseDontDestroyOnLoad) {
                if (transform.parent != null && transform.root != null) {
                    DontDestroyOnLoad(transform.root.gameObject);
                } else {
                    DontDestroyOnLoad(gameObject);
                }
            }
        }
    }
}