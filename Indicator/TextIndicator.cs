using System;
using TMPro;
using UnityEngine;

namespace minyee2913.Utils {
    public class TextIndicator : MonoBehaviour
    {
        IndicatorSetting setting;
        public string message;
        public Color color;
        float time;
        bool alive;
        TMP_Text text;
        public Action<TextIndicator> onTimeEnd;

        public void Active(IndicatorSetting setting) {
            if (text == null)
                text = gameObject.AddComponent<TextMeshPro>();
                
            this.setting = setting;
            alive = true;
            time = 0;

            text.font = setting.font;
            text.fontSize = setting.fontScale;
        }

        void FixedUpdate()
        {
            if (!alive)
                return;

            text.text = message;
            text.color = color;

            if (time > setting.textLifeTime) {
                onTimeEnd?.Invoke(this);
                alive = false;
            }
        }
    }
}
