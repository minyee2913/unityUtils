using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace minyee2913.Utils
{
    //덧셈으로 기초 값을 증가시킵니다.
    public enum StatMathType
    {
        Add, //덧셈으로 기초 값을 증가시킵니다.
        Remove, //뺄셈으로 기초 값을 감소시킵니다.
        Increase, //백분율에 기반하여 값을 증가시킵니다.
        Decrease //백분율에 기반하여 값을 감소시킵니다.
    }
    public class Buf
    {
        public string Id;
        public string Comment;
        public string key;
        public StatMathType mathType;
        public float value;
    }

    [System.Serializable]
    public struct StatDisplay
    {
        public string Key;
        public float Value;
    }

    public class StatController : MonoBehaviour
    {
        [SerializeField]
        StatBaseConstructor constructor;

        Dictionary<string, float> statBase = new();
        Dictionary<string, float> statResult = new();
        Dictionary<string, Buf> bufs = new();
        [SerializeField]
        List<StatBaseField> overrideFields;

        [SerializeField]
        List<StatDisplay> display = new();

        void Awake()
        {
            if (constructor == null)
                LoadDefaultConstructor();

            ConstructBase(constructor);
        }

        void FixedUpdate()
        {
            display.Clear();
            foreach (KeyValuePair<string, float> pair in statResult)
            {
                display.Add(new StatDisplay() { Key = pair.Key, Value = pair.Value });
            }
        }

        void LoadDefaultConstructor()
        {
            string path = "StatBase/defaultConstructor";

            constructor = Resources.Load<StatBaseConstructor>(path);
        }

        public void ConstructBase(StatBaseConstructor constructor)
        {
            foreach (StatBaseField field in constructor.fields)
            {
                statBase[field.key] = field.defaultValue;
            }

            foreach (StatBaseField field in overrideFields)
            {
                statBase[field.key] = field.defaultValue;
            }

            statResult = statBase;
        }

        public Buf GetBuf(string key)
        {
            return bufs[key];
        }

        public Dictionary<string, Buf> GetBufs()
        {
            return bufs;
        }

        public void AddBuf(string id, Buf buf)
        {
            buf.Id = id;
            bufs[id] = buf;
        }

        public Dictionary<string, float> GetBase()
        {
            return statBase;
        }

        public Dictionary<string, float> GetResult()
        {
            return statResult;
        }

        public float GetResultValue(string key)
        {
            return statResult[key];
        }

        public float GetBaseValue(string key)
        {
            return statBase[key];
        }

        public float Calc(string key)
        {
            float value = 0;
            float per = 0;

            if (statBase.ContainsKey(key))
            {
                value = statBase[key];
            }

            foreach (KeyValuePair<string, Buf> pair in bufs)
            {
                switch (pair.Value.mathType)
                {
                    case StatMathType.Add:
                        value += pair.Value.value;
                        break;
                    case StatMathType.Remove:
                        value -= pair.Value.value;
                        break;
                    case StatMathType.Increase:
                        per += pair.Value.value;
                        break;
                    case StatMathType.Decrease:
                        per -= pair.Value.value;
                        break;
                }
            }

            value *= 1 + per / 100;

            statResult[key] = value;

            return value;
        }

#if UNITY_EDITOR
        [CustomEditor(typeof(StatController)), CanEditMultipleObjects]
        public class StatControllerEditor : Editor
        {
            bool bufFoldout;
            public override void OnInspectorGUI()
            {
                base.OnInspectorGUI();

                StatController controller = (StatController)target;

                bufFoldout = EditorGUILayout.Foldout(bufFoldout, "버프", true);
                if (bufFoldout)
                {
                    string tx = "";

                    foreach (KeyValuePair<string, Buf> pair in controller.GetBufs())
                    {
                        string Main = pair.Key.ToUpper() + " " + pair.Value.value.ToString();

                        if (pair.Value.Comment != "")
                        {
                            Main = pair.Value.Comment;
                        }

                        tx += Main + "\n- [" + pair.GetType().ToString() + "]" + pair.Key + ": " + pair.Value.ToString() + "\n";
                    }

                    GUIStyle codeStyle = new GUIStyle(EditorStyles.helpBox);
                    codeStyle.font = EditorStyles.miniFont; // 혹은 `EditorStyles.label.font`으로
                    codeStyle.richText = true;
                    codeStyle.alignment = TextAnchor.MiddleLeft;
                    codeStyle.padding = new RectOffset(6, 6, 2, 2);
                    codeStyle.normal.textColor = Color.white;

                    if (tx == "")
                    {
                        tx = "<color='grey'>버프 효과가 없습니다.</color>";
                    }

                    GUILayout.Label(tx, codeStyle);

                    if (GUILayout.Button("버프 추가"))
                    {
                        Rect buttonRect = GUILayoutUtility.GetLastRect();
                        PopupWindow.Show(buttonRect, new AddBufPopup());
                    }
                }
            }
            
            public class AddBufPopup : PopupWindowContent
            {
                string key, comment;
                string value;
                float floatVal;
                public override Vector2 GetWindowSize()
                {
                    return new Vector2(200, 100);
                }

                public override void OnGUI(Rect rect)
                {
                    GUILayout.Label("추가할 버프 정보 설정", EditorStyles.boldLabel);
                    comment = EditorGUILayout.TextField("Comment", comment);
                    key = EditorGUILayout.TextField("Key", key);
                    value = EditorGUILayout.TextField("value", value);

                    if (float.TryParse(value, out float parsed))
                    {
                        floatVal = parsed;
                        EditorGUILayout.LabelField("현재 값", floatVal.ToString());
                    }
                    else
                    {
                        EditorGUILayout.HelpBox("올바른 숫자가 아닙니다.", MessageType.Warning);
                    }


                    if (GUILayout.Button("닫기"))
                    {
                        editorWindow.Close();
                    }
                }
            }
        }
        #endif
    }

}