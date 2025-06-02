using System;
using System.Collections.Generic;
using UnityEngine;

namespace minyee2913.Utils {
    public class HealthObject : MonoBehaviour
    {
        public enum Cause
        {
            None,
            Melee,
            Range,
            Skill,
            Aditional,
            More,
        }
        public int MaxHealth, Health;

        public float Rate => (float)Health / MaxHealth;

        public bool isDeath;

        public class OnDamageEv {
            public int Damage;
            public HealthObject attacker;
            public Cause cause;
            public bool cancel;
        }

        List<Action<OnDamageEv>> OnDamageEvents = new();
        List<Action<OnDamageEv>> onDeathEvents = new();

        public void ResetToMax() {
            Health = MaxHealth;
        }

        public void OnDamage(Action<OnDamageEv> ev) {
            OnDamageEvents.Add(ev);
        }
        public void OnDeath(Action<OnDamageEv> ev) {
            onDeathEvents.Add(ev);
        }

        public bool GetDamage(int damage, HealthObject attacker, Cause cause = Cause.None) {
            if (isDeath)
                return false;

            OnDamageEv ev = new()
            {
                Damage = damage,
                attacker = attacker,
                cause = cause,
            };

            foreach (var _ev in OnDamageEvents) {
                _ev.Invoke(ev);
            }

            if (ev.cancel) {
                return false;
            }

            Health -= ev.Damage;

            if (Health <= 0) {
                isDeath = true;

                foreach (var _ev in onDeathEvents) {
                    _ev.Invoke(ev);
                }
            }

            return true;
        }
    }
}
