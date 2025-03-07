using System.Collections.Generic;
using UnityEngine;

namespace minyee2913.Utils {
    //덧셈으로 기초 값을 증가시킵니다.
    public enum StatMathType {
        Add, //덧셈으로 기초 값을 증가시킵니다.
        Remove, //뺄셈으로 기초 값을 감소시킵니다.
        Increase, //백분율에 기반하여 값을 증가시킵니다.
        Decrease //백분율에 기반하여 값을 감소시킵니다.
    }
    public class Buf {
        public string Id;
        public string Comment;
        public string key;
        public StatMathType mathType;
        public float value;
    }

    public class StatController : MonoBehaviour
    {
        [SerializeField]
        StatBaseConstructor constructor;

        Dictionary<string, float> statBase = new();
        Dictionary<string, float> statResult = new();
        Dictionary<string, Buf> bufs = new();

        void Awake()
        {
            if (constructor == null)
                LoadDefaultConstructor();

            ConstructBase(constructor);
        }

        void LoadDefaultConstructor() {
            string path = "StatBase/defaultConstructor";

            constructor = Resources.Load<StatBaseConstructor>(path);
        }

        public void ConstructBase(StatBaseConstructor constructor) {
            foreach (StatBaseField field in constructor.fields) {
                statBase[field.key] = field.defaultValue;
            }
        }

        public Buf GetBuf(string key) {
            return bufs[key];
        }

        public void AddBuf(string id, Buf buf) {
            buf.Id = id;
            bufs[id] = buf;
        }

        public Dictionary<string, float> GetBase() {
            return statBase;
        }

        public Dictionary<string, float> GetResult() {
            return statResult;
        }

        public float GetResultValue(string key) {
            return statResult[key];
        }

        public float GetBaseValue(string key) {
            return statBase[key];
        }

        public float Calc(string key) {
            float value = 0;
            float per = 0;

            if (statBase.ContainsKey(key)) {
                value = statBase[key];
            }

            foreach (KeyValuePair<string, Buf> pair in bufs) {
                switch (pair.Value.mathType) {
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

            value *= 1 + per/100;

            statResult[key] = value;

            return value;
        }
    }

}