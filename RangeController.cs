using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace minyee2913.Utils {
    public enum RangeShape {
        Cube,
        Sphere,
    }
    [System.Serializable]
    public struct TargetRange {
        public string Name;
        public Vector3 offset, size;
        public bool ShowGizmos;
        public Color GizmosColor;
        public RangeShape shape;
    }

    public class RangeController : MonoBehaviour {
        [SerializeField]
        Transform origin;

        public bool useForward;
        public List<TargetRange> ranges = new();
        public TargetRange GetRange(string name) {
            return ranges.Find((r) => r.Name == name);
        }

        public TargetRange ScaledRange(string name, float scale) {
            TargetRange r = GetRange(name);
            r.size *= scale;
            return r;
        }

        /// <summary>Sphere: radius. Cube: max horizontal half-extent (distance check용).</summary>
        public float RangeReach(TargetRange range) {
            if (range.shape == RangeShape.Sphere)
                return range.size.x;
            return Mathf.Max(range.size.x, range.size.z) * 0.5f;
        }

        Vector3 GetWorldOffset(Vector3 rawOffset) {
            Vector3 localOffset = new Vector3(rawOffset.x * -origin.localScale.x, rawOffset.y, rawOffset.z);
            return useForward ? transform.TransformDirection(localOffset) : localOffset;
        }

        static bool IsPlayerLayerMask(LayerMask mask) =>
            (mask.value & LayerMask.GetMask("player")) != 0;

        static bool ShouldSupplementPlayersOnServer(LayerMask mask) =>
            IsPlayerLayerMask(mask) &&
            LanManager.IsMulti &&
            NetworkManager.Singleton != null &&
            NetworkManager.Singleton.IsServer;

        void SupplementPlayerTransforms(Vector3 center, TargetRange range, List<Transform> targets)
        {
            float reach = RangeReach(range) + 2.5f;
            foreach (var pc in PlayerController.players)
            {
                if (pc == null || pc.battle == null || pc.battle.health == null)
                    continue;
                if (pc.battle.health.isDeath)
                    continue;
                if (Vector3.Distance(pc.transform.position, center) > reach)
                    continue;
                if (!targets.Contains(pc.transform))
                    targets.Add(pc.transform);
            }
        }

        public static NetworkHealthObject ResolvePlayerHealth(Transform t)
        {
            if (t == null)
                return null;

            var hp = t.GetComponent<NetworkHealthObject>();
            return hp != null ? hp : t.GetComponentInParent<NetworkHealthObject>();
        }

        public List<Transform> GetHitInRangeFreely(Vector3 center, TargetRange range, LayerMask mask) {
            List<Transform> targets = new();

            if ((mask.value & LayerMask.GetMask("area")) != 0)
            {
                PlayerBattle battle = PlayerController.Local != null ? PlayerController.Local.battle : null;
                if (battle != null && battle.inArea && battle.areas != null)
                {
                    foreach (Area ar in battle.areas.QueryRange(center, range))
                        targets.Add(ar.transform);
                    return targets;
                }
            }

            Quaternion rotation = useForward ? transform.rotation : Quaternion.identity;

            Collider[] colliders = null;

            if (range.shape == RangeShape.Cube) {
                colliders = Physics.OverlapBox(center, range.size * 0.5f, rotation, mask);
            } else if (range.shape == RangeShape.Sphere) {
                colliders = Physics.OverlapSphere(center, range.size.x, mask);
            }

            if (colliders != null)
            {
                foreach (var col in colliders)
                {
                    if (col.transform == transform)
                        continue;

                    targets.Add(col.transform);
                }
            }

            if (ShouldSupplementPlayersOnServer(mask))
                SupplementPlayerTransforms(center, range, targets);

            return targets;
        }

        public List<Transform> GetHitInRange(TargetRange range, LayerMask mask) {
            Vector3 offset = GetWorldOffset(range.offset);
            Vector3 center = transform.position + offset;
            return GetHitInRangeFreely(center, range, mask);
        }

        public Vector3 GetRangeWorldCenter(TargetRange range)
        {
            Vector3 offset = GetWorldOffset(range.offset);
            return transform.position + offset;
        }

        /// <summary>플레이어 타격 판정 — Physics + 헤드리스 서버 거리 폴백.</summary>
        public List<NetworkHealthObject> GetPlayerHealthInRange(TargetRange range)
        {
            var result = new List<NetworkHealthObject>();
            var seen = new HashSet<NetworkHealthObject>();

            foreach (var t in GetHitInRange(range, LayerMask.GetMask("player")))
            {
                var hp = ResolvePlayerHealth(t);
                if (hp != null && seen.Add(hp))
                    result.Add(hp);
            }

            return result;
        }

        public List<NetworkHealthObject> GetPlayerHealthInRangeFreely(Vector3 center, TargetRange range)
        {
            var result = new List<NetworkHealthObject>();
            var seen = new HashSet<NetworkHealthObject>();

            foreach (var t in GetHitInRangeFreely(center, range, LayerMask.GetMask("player")))
            {
                var hp = ResolvePlayerHealth(t);
                if (hp != null && seen.Add(hp))
                    result.Add(hp);
            }

            return result;
        }

        public List<Transform> GetHitInRangeFreely2D(Vector3 center3D, TargetRange range, LayerMask mask)
        {
            Vector2 center = (Vector2)center3D; // z 제거
            List<Transform> targets = new();
            Collider2D[] colliders = null;

            float angle = useForward ? transform.eulerAngles.z : 0f;

            if (range.shape == RangeShape.Cube)
            {
                Vector2 boxSize = new Vector2(range.size.x, range.size.y);
                colliders = Physics2D.OverlapBoxAll(center, boxSize, angle, mask);
            }
            else if (range.shape == RangeShape.Sphere)
            {
                colliders = Physics2D.OverlapCircleAll(center, range.size.x, mask);
            }

            if (colliders == null)
            {
                Debug.LogWarning("No colliders detected — Check Z=0, LayerMask, and Collider2D presence.");
                return targets;
            }

            foreach (var col in colliders)
            {
                if (col.transform == transform)
                    continue;

                targets.Add(col.transform);
            }

            return targets;
        }

        public List<Transform> GetHitInRange2D(TargetRange range, LayerMask mask) {
            Vector3 offset = GetWorldOffset(range.offset);
            Vector3 center = transform.position + offset;
            return GetHitInRangeFreely2D(center, range, mask);
        }

        void OnDrawGizmos() {
            if (origin == null) return;

            foreach (var range in ranges) {
                if (range.ShowGizmos) {
                    Gizmos.color = range.GizmosColor;

                    Vector3 offset = GetWorldOffset(range.offset);
                    Vector3 gizmoPos = transform.position + offset;

                    if (range.shape == RangeShape.Cube) {
                        if (useForward) {
                            Gizmos.matrix = Matrix4x4.TRS(gizmoPos, transform.rotation, Vector3.one);
                            Gizmos.DrawWireCube(Vector3.zero, range.size);
                            Gizmos.matrix = Matrix4x4.identity;
                        } else {
                            Gizmos.DrawWireCube(gizmoPos, range.size);
                        }
                    } else if (range.shape == RangeShape.Sphere) {
                        Gizmos.DrawWireSphere(gizmoPos, range.size.x);
                    }
                }
            }
        }
    }
}
