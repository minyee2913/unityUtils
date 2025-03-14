using TMPro;
using UnityEngine;

namespace minyee2913.Utils {
    [CreateAssetMenu(fileName = "IndicatorSetting", menuName = "2913Utils/indicatorSetting", order = int.MaxValue)]
    public class IndicatorSetting : ScriptableObject
    {
        [Header("Text")]
        public TMP_FontAsset font;
        public float fontScale;
        public float textLifeTime;
    }
}
