using System.Collections.Generic;
using UnityEngine;

namespace minyee2913.Utils {
    [System.Serializable]
    public struct StatBaseField {
        public string key;
        public float defaultValue;
    }
    [CreateAssetMenu(fileName = "StatBaseConstructor", menuName = "2913Utils/statBaseConstructor", order = int.MaxValue)]
    public class StatBaseConstructor : ScriptableObject
    {
        public List<StatBaseField> fields;
    }
}
