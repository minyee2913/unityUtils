using UnityEngine;

[DisallowMultipleComponent]
[AddComponentMenu("")]
public class LevelPaintMarker : MonoBehaviour
{
	public Vector3 size;
	public GameObject prefab;
	public int paletteIndex = -1;
	public Vector3 worldCenter;
	public LevelPrefabPalette paletteAsset;
}


