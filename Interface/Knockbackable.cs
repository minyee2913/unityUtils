using UnityEngine;

namespace minyee2913.Utils {
    public interface Knockbackable {
        bool GiveKnockback(float power, float height, int direction);
        bool GiveKnockback(float power, float height, Vector3 center);
    }
}
