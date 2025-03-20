using System.Collections.Generic;
using UnityEngine;

namespace minyee2913.Utils {
    public abstract class UIBasePanel : MonoBehaviour
    {
        static List<UIBasePanel> opened = new();
        public bool IsUppestLayer {
            get {
                if (IsOpened) {
                    if (opened.IndexOf(this) == opened.Count - 1) {
                        return true;
                    }
                }
                return false;
            }
        }
        public bool IsOpened => opened.Contains(this);
        [SerializeField]
        bool openByScale;
        protected float closedScaleRate => 0;
        protected float openedScaleRate => 1;
        protected float transitionTime => 0.3f;
        Vector2 defaultScale;

        void Awake()
        {
            defaultScale = transform.localScale;
        }

        public static void CloseAll() {
            foreach (UIBasePanel panel in opened) {
                panel.Close();
            }
        }

        public virtual void Open() {
            if (openByScale) {
                LeanTween.scale(gameObject, defaultScale, transitionTime).setEase(LeanTweenType.easeOutCirc);
            } else {
                gameObject.SetActive(true);
            }

            opened.Add(this);
        }

        public virtual void Close() {
            if (openByScale) {
                LeanTween.scale(gameObject, defaultScale * closedScaleRate, transitionTime).setEase(LeanTweenType.easeOutCirc);
            } else {
                gameObject.SetActive(false);
            }

            opened.Remove(this);
        }

        void OnDestroy()
        {
            if (opened.Contains(this)) {
                opened.Remove(this);
            }
        }
    }

}