using UnityEngine;

public class SpinObject : MonoBehaviour
{
    [SerializeField]
    bool useRealtime;
    [SerializeField]
    Vector3 spinVector;

    void Update()
    {
        if (useRealtime)
        {
            transform.Rotate(spinVector * Time.unscaledDeltaTime);
        }
        else
        {
            transform.Rotate(spinVector * Time.deltaTime);
        }
    }
}
