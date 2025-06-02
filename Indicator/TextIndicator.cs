using System;
using TMPro;
using UnityEngine;

namespace minyee2913.Utils {
    [ExecuteAlways]
    public class TextIndicator : MonoBehaviour
    {
        IndicatorSetting setting;
        public string message;
        public Color color;
        float time;
        bool alive;
        TMP_Text text;
        public Action<TextIndicator> onTimeEnd;

        public void Active(IndicatorSetting setting)
        {
            if (text == null)
                text = gameObject.AddComponent<TextMeshPro>();

            this.setting = setting;
            alive = true;
            time = 0;

            text.font = setting.font;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.fontSize = setting.fontScale;
            // ✅ 렌더링이 되도록 머티리얼 설정
            if (text.font != null && text.font.material != null)
                text.fontSharedMaterial = text.font.material;

            // ✅ 기본 정렬 및 기타 설정
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;

            // ✅ Transform 설정: 스케일과 위치
            text.rectTransform.localScale = Vector3.one * 0.1f; // 너무 크면 안 보임
            text.transform.localPosition = Vector3.zero;
        }

        void Update()
        {
            if (!alive)
                return;

            text.text = message;
            text.color = color;

            if (time > setting.textLifeTime)
            {
                onTimeEnd?.Invoke(this);
                alive = false;
            }

            time += Time.deltaTime;
        }
    }
}
