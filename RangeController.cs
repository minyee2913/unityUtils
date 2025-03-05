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
        public List<TargetRange> ranges = new();
        public TargetRange GetRange(string name) {
            return ranges.Find((r)=>r.Name == name);
        }

        public List<Transform> GetHitInRange(TargetRange range, LayerMask mask) {
            Vector3 offset = new Vector3(range.offset.x * -origin.localScale.x, range.offset.y, range.offset.z);

            RaycastHit[] hit = {};
            List<Transform> targets = new();

            if (range.shape == RangeShape.Cube) {
                hit = Physics.BoxCastAll(transform.position + offset, range.size, Vector3.up, Quaternion.identity, Mathf.Infinity, mask);
            } else if (range.shape == RangeShape.Sphere) {
                hit = Physics.SphereCastAll(transform.position + offset, range.size.x, Vector3.up, Mathf.Infinity, mask);
            }

            foreach (RaycastHit _hit in hit) {
                if (transform == _hit.transform)
                    continue;

                targets.Add(_hit.transform);
            }

            return targets;
        }

        void OnDrawGizmos()
        {

            foreach (var range in ranges) {
                if (range.ShowGizmos) {
                    Gizmos.color = range.GizmosColor;

                    Vector3 offset = new Vector3(range.offset.x * -origin.localScale.x, range.offset.y, range.offset.z);

                    if (range.shape == RangeShape.Cube) {
                        Gizmos.DrawWireCube(transform.position + offset, range.size);
                    } else if (range.shape == RangeShape.Sphere) {
                        Gizmos.DrawWireSphere(transform.position + offset, range.size.x);
                    }
                }
            }
        }
    }
}
