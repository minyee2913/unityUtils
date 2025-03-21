using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

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

        #region PROTECTED / PRIVATE
        protected virtual bool openByScale => true;
        protected virtual bool closeByEscape => true;
        protected virtual float closedScaleRate => 0;
        protected virtual float openedScaleRate => 1;
        protected virtual float transitionTime => 0.3f;
        Vector3 defaultScale;
        #endregion
        [SerializeField]
        bool Opened;

        void Awake()
        {
            defaultScale = transform.localScale;
        }

        void Update()
        {
            if (IsOpened && closeByEscape && IsUppestLayer) {
                if (Input.GetKeyDown(KeyCode.Escape)) {
                    Close();
                }
            }

            Opened = IsOpened;

            PanelUpdate();
        }

        protected virtual void PanelUpdate() {}

        public static void CloseAll() {
            foreach (UIBasePanel panel in opened) {
                panel.Close();
            }
        }

        public virtual void Open() {
            if (openByScale) {
                transform.DOScale(defaultScale * openedScaleRate, transitionTime).SetEase(Ease.OutCirc);
            } else {
                gameObject.SetActive(true);
            }

            opened.Add(this);
        }

        public virtual void Close() {
            if (openByScale) {
                transform.DOScale(defaultScale * closedScaleRate, transitionTime).SetEase(Ease.OutCirc);
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