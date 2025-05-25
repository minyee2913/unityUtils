using System;
using UnityEngine;

namespace minyee2913.Utils {
    public class Cooldown {
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
            if (!isIn) return 0f;

            DateTime endTime = startTime.AddSeconds(time);
            TimeSpan remaining = endTime - DateTime.Now;

            return Mathf.Max(0f, (float)remaining.TotalSeconds);
        }

        public bool IsIn() {
            if (isIn) {
                if (startTime.AddSeconds(time) < DateTime.Now) {
                    isIn = false;
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