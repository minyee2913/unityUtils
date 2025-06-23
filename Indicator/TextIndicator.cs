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
        public Vector3 pos;
        float time;
        bool alive;
        TMP_Text text;
        public Action<TextIndicator> onTimeEnd;
        Renderer mesh;

        public void Active(IndicatorSetting setting)
        {
            if (text == null)
                text = gameObject.AddComponent<TextMeshPro>();
            if (mesh == null)
                mesh = GetComponent<Renderer>();

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

            transform.position = pos;

            mesh.sortingLayerName = setting.sortingLayer;
            mesh.sortingOrder = setting.sortOrder;
        }

        float DeltaTime()
        {
            if (setting.useRealtime)
                return Time.unscaledDeltaTime;
            else
                return Time.deltaTime;
        }

        void Update()
        {
            if (!alive || !text)
                return;

            text.text = message;
            text.color = color;

            if (time > setting.textLifeTime)
            {
                onTimeEnd?.Invoke(this);
                alive = false;
            }

            if (setting.lookCamera)
            {
                transform.LookAt(Camera.main.transform);
                transform.Rotate(180, 0, 180);
            }

            switch (setting.anim)
            {
                case IndicatorAnim.FlowUp:
                    transform.position += new Vector3(0, setting.animScale) * DeltaTime();
                    break;
                case IndicatorAnim.FlowDown:
                    transform.position += new Vector3(0, setting.animScale) * DeltaTime();
                    break;
                case IndicatorAnim.FlowSin:
                    transform.position = pos + new Vector3(0, Mathf.Sin(setting.animScale * time) * setting.sinRange) * DeltaTime();
                    break;
            }

            time += DeltaTime();
        }
    }
}
