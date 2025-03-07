using UnityEngine;

namespace minyee2913.Utils {
    public interface Knockbackable {
        bool GiveKnockback(float power, float height, int direction);
    }
}
