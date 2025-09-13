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

        public class OnDamageEv
        {
            public int Damage;
            public HealthObject attacker;
            public Cause cause;
            public bool cancel;
            public float critPer, critMultiple;
        }
        public class OnGiveDamageEv
        {
            public int Damage;
            public HealthObject target;
            public Cause cause;
            public float increaseDamage;
        }

        public class OnDamageFinalEv
        {
            public int Damage;
            public HealthObject attacker;
            public Cause cause;
            public float critPer, critMultiple;
            public bool isCrit;

            public OnDamageFinalEv(OnDamageEv ev)
            {
                Damage = ev.Damage;
                attacker = ev.attacker;
                cause = ev.cause;
                critPer = ev.critPer;
                critMultiple = ev.critMultiple;
            }
        }

        public class OnHealEv
        {
            public int value;
            public HealthObject healer;
            public bool cancel;
        }

        [SerializeField]
        bool SyncToStat;

        List<Action<OnDamageEv>> OnDamageEvents = new();
        List<Action<OnGiveDamageEv>> OnGiveDamageEvents = new();
        List<Action<OnGiveDamageEv>> OnGiveDamageFinalEvents = new();
        List<Action<OnGiveDamageEv>> OnKillEvents = new();
        List<Action<OnDamageFinalEv>> OnDamageFinalEvents = new();
        List<Action<OnDamageEv>> onDeathEvents = new();
        List<Action<OnHealEv>> onHealEvents = new();
        StatController stat;
        void Awake()
        {
            stat = GetComponent<StatController>();
        }

        void Update()
        {
            if (stat != null && SyncToStat)
            {
                int maxHealth = (int)stat.GetResultValue("maxHealth");

                if (MaxHealth != maxHealth)
                {
                    ChangeMax(maxHealth);
                }
            }
        }

        public void ResetToMax()
        {
            Health = MaxHealth;
            isDeath = false;
        }

        public void Kill()
        {
            Health = 0;
            isDeath = true;
        }

        public void ChangeMax(int maxHealth)
        {
            float newHealth = Rate * maxHealth;
            MaxHealth = maxHealth;
            Health = (int)newHealth;
        }

        public void OnDamage(Action<OnDamageEv> ev)
        {
            OnDamageEvents.Add(ev);
        }
        public void OnGiveDamage(Action<OnGiveDamageEv> ev)
        {
            OnGiveDamageEvents.Add(ev);
        }
        public void OnGiveDamageFinal(Action<OnGiveDamageEv> ev)
        {
            OnGiveDamageFinalEvents.Add(ev);
        }
        public void OnKill(Action<OnGiveDamageEv> ev)
        {
            OnKillEvents.Add(ev);
        }
        public void OnDamageFinal(Action<OnDamageFinalEv> ev)
        {
            OnDamageFinalEvents.Add(ev);
        }
        public void OnDeath(Action<OnDamageEv> ev)
        {
            onDeathEvents.Add(ev);
        }
            public void OnHeal(Action<OnHealEv> ev) {
            onHealEvents.Add(ev);
        }

        public bool Heal(int val, HealthObject healer = null)
        {
            if (isDeath)
                return false;

            OnHealEv ev = new()
            {
                value = val,
                healer = healer,
            };

            foreach (var _ev in onHealEvents)
            {
                _ev.Invoke(ev);
            }

            if (ev.cancel)
            {
                return false;
            }

            Health += ev.value;

            if (Health > MaxHealth)
            {
                Health = MaxHealth;
            }

            return true;
        }

        public int GetDamage(int damage, HealthObject attacker, Cause cause = Cause.None)
        {
            if (isDeath)
            {
                return -1;
            }

            if (attacker != null)
            {
                OnGiveDamageEv Tev = new()
                {
                    Damage = damage,
                    target = this,
                    cause = cause,
                };

                foreach (var _ev in attacker.OnGiveDamageEvents)
                {
                    _ev.Invoke(Tev);
                }

                damage = (int)(Tev.Damage * (1 + Tev.increaseDamage * 0.01f));
            }

            OnDamageEv ev = new()
            {
                Damage = damage,
                attacker = attacker,
                cause = cause,
            };

            if (stat != null)
            {
                ev.critPer = stat.GetResultValue("crit");
                ev.critMultiple = stat.GetResultValue("critMultiple");
            }

            foreach (var _ev in OnDamageEvents)
            {
                _ev.Invoke(ev);
            }

            if (ev.cancel)
            {
                return -1;
            }

            OnDamageFinalEv final = new(ev);

            float finalDam = ev.Damage;

            if (UnityEngine.Random.Range(0, 100f) <= final.critPer)
            {
                finalDam *= 1 + final.critMultiple * 0.01f;
                final.isCrit = true;
            }

            final.Damage = (int)finalDam;

            if (final.Damage <= 0)
                final.Damage = 1;

            foreach (var _ev in OnDamageFinalEvents)
                {
                    _ev.Invoke(final);
                }

            Health -= final.Damage;

            if (attacker != null)
            {
                OnGiveDamageEv Tev = new()
                {
                    Damage = final.Damage,
                    target = this,
                    cause = final.cause,
                };

                foreach (var _ev in attacker.OnGiveDamageFinalEvents)
                {
                    _ev.Invoke(Tev);
                }
            }

            if (Health <= 0)
            {
                isDeath = true;

                OnGiveDamageEv Tev = new()
                {
                    Damage = damage,
                    target = this,
                    cause = cause,
                };

                if (attacker != null)
                {
                    foreach (var _ev in attacker.OnKillEvents)
                    {
                        _ev?.Invoke(Tev);
                    }
                }

                foreach (var _ev in onDeathEvents)
                    {
                        _ev?.Invoke(ev);
                    }
            }

            return final.Damage;
        }
    }
}
