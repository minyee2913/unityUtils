using System;

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