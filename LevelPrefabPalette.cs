using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Utils/Level Prefab Palette", fileName = "LevelPrefabPalette")]
public class LevelPrefabPalette : ScriptableObject
{
	[System.Serializable]
	public class PrefabItem
	{
		public GameObject prefab;
		public Vector3 size;
	}

	// Legacy support
	public List<GameObject> prefabs = new List<GameObject>();

	// New structured palette with sizes
	public List<PrefabItem> items = new List<PrefabItem>();
}


