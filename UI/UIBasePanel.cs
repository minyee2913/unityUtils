using System.Collections.Generic;
using UnityEngine;

namespace minyee2913.Utils {
    public abstract class UIBasePanel : MonoBehaviour
    {
        #region STATIC
        static List<UIBasePanel> opened = new();
        public static int PanelCount => opened.Count;
        public static UIBasePanel GetUppestPanel() {
            if (opened.Count >= 1) {
                return opened[opened.Count - 1];
            }

            return null;
        }
        #endregion

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

        #region SERIALIZED
        [SerializeField]
        bool openByScale;
        #endregion

        #region PROTECTED / PRIVATE
        protected float closedScaleRate => 0;
        protected float openedScaleRate => 1;
        protected float transitionTime => 0.3f;
        Vector2 defaultScale;
        #endregion

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