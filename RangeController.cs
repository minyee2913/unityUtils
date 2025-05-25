using System.Collections.Generic;
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

        Vector3 GetWorldOffset(Vector3 rawOffset) {
            Vector3 localOffset = new Vector3(rawOffset.x * -origin.localScale.x, rawOffset.y, rawOffset.z);
            return useForward ? transform.TransformDirection(localOffset) : localOffset;
        }

            public List<Transform> GetHitInRangeFreely(Vector3 center, TargetRange range, LayerMask mask) {
            Quaternion rotation = useForward ? transform.rotation : Quaternion.identity;

            Collider[] colliders = null;
            List<Transform> targets = new();

            if (range.shape == RangeShape.Cube) {
                colliders = Physics.OverlapBox(center, range.size * 0.5f, rotation, mask);
            } else if (range.shape == RangeShape.Sphere) {
                colliders = Physics.OverlapSphere(center, range.size.x, mask);
            }

            foreach (var col in colliders) {
                if (col.transform == transform)
                    continue;

                targets.Add(col.transform);
            }

            return targets;
        }
        public List<Transform> GetHitInRange(TargetRange range, LayerMask mask) {
            Vector3 offset = GetWorldOffset(range.offset);
            Vector3 center = transform.position + offset;
            return GetHitInRangeFreely(center, range, mask);
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
