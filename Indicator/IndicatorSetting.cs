using TMPro;
using UnityEngine;

namespace minyee2913.Utils {
    public enum IndicatorAnim
    {
        FlowUp,
        FlowDown,
        FlowSin
    }
    [CreateAssetMenu(fileName = "IndicatorSetting", menuName = "2913Utils/indicatorSetting", order = int.MaxValue)]
    public class IndicatorSetting : ScriptableObject
    {
        [Header("Text")]
        public TMP_FontAsset font;
        public float fontScale;
        public float textLifeTime;
        public IndicatorAnim anim;
        public float animScale;
        public float sinRange;
        public string sortingLayer;
        public int sortOrder;
        public bool useRealtime;
        public bool lookCamera;
    }
}
