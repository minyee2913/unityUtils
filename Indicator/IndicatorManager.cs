using System.Collections.Generic;
using minyee2913.Utils;
using UnityEditor;
using UnityEngine;

namespace minyee2913.Utils {
    public class IndicatorManager : Singleton<IndicatorManager>
    {
        [SerializeField]
        int maxSaveCount = 8;
        public IndicatorSetting setting;
        [SerializeField]
        List<TextIndicator> textPoolings = new();
        List<TextIndicator> textIndicated = new();
        public string test_text_message;
        public Vector3 test_generate_position;

        void Awake()
        {
            if (setting == null)
                LoadDefaultSetting();
        }

        public void LoadDefaultSetting() {
            string path = "IndicatorSettings/defaultSetting";

            setting = Resources.Load<IndicatorSetting>(path);
        }

        public TextIndicator GenerateText(string text, Vector3 position, Color color, IndicatorSetting setting = null) {
            if (setting == null) {
                setting = this.setting;
            }

            TextIndicator indicator;
            if (textPoolings.Count > 0) {
                indicator = textPoolings[0];
                textPoolings.Remove(indicator);
            } else {
                GameObject obj = new("textIndicator");
                obj.transform.SetParent(transform);

                indicator = obj.AddComponent<TextIndicator>();
            }

            indicator.message = text;
            indicator.color = color;
            indicator.transform.position = position;

            indicator.onTimeEnd = TextDispose;
            indicator.Active(setting);

            textIndicated.Add(indicator);

            return indicator;
        }

        void TextDispose(TextIndicator indicator) {
            textIndicated.Remove(indicator);

            if (textPoolings.Count >= maxSaveCount) {
                Destroy(indicator.gameObject);
            } else {
                textPoolings.Add(indicator);
                indicator.gameObject.SetActive(false);
            }
        }
    }

    #if UNITY_EDITOR
    [CustomEditor(typeof(IndicatorManager))]
    public class IndicatorManagerEditor : Editor {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            IndicatorManager manager = (IndicatorManager)target;

            GUILayout.Space(10);

            if (GUILayout.Button("load defaultSetting")) {
                manager.LoadDefaultSetting();
            }

            GUILayout.Space(10);

            if (GUILayout.Button("generate Text")) {
                manager.GenerateText(manager.test_text_message, manager.test_generate_position, Color.white);
            }
        }
    }
    #endif
}