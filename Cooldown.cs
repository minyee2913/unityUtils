using System;

namespace minyee2913.Utils {
    class Cooldown {
        bool isIn;
        public float time;
        public DateTime startTime;
        public Cooldown(float time) {
            this.time = time;
        }

        public void Start() {
            startTime = DateTime.Now;

            isIn = true;
        }

        public float timeLeft() {
            var t = startTime.AddMilliseconds(time * 1000);
            DateTime now = DateTime.Now;
            return (float)(t.Millisecond - now.Millisecond) / 1000;
        }

        public bool IsIn() {
            if (isIn) {
                if (startTime.AddSeconds(time) < DateTime.Now) {
                    return false;
                } else {
                    return true;
                }
            } else {
                return false;
            }
        }
    }
}