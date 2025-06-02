using UnityEngine;

namespace minyee2913.Utils
{
    [ExecuteAlways]
    public class SetLayer : MonoBehaviour
    {
        public string sortingLayer;
        public int sortingOrder;

        void Start()
        {
            Renderer mesh = GetComponent<Renderer>();
            mesh.sortingLayerName = sortingLayer;
            mesh.sortingOrder = sortingOrder;

            SetChild(transform);
        }

        void SetChild(Transform tr)
        {
            foreach (Transform child in tr)
            {
                Renderer mesh_ = child.GetComponent<Renderer>();

                if (mesh_ != null)
                {
                    mesh_.sortingLayerName = sortingLayer;
                    mesh_.sortingOrder = sortingOrder;

                    SetChild(child);
                }
            }
        }
    }
}