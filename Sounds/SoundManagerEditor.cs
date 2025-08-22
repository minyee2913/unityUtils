#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace minyee2913.Utils
{
    [CustomEditor(typeof(SoundManager))]
    public class SoundManagerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            SoundManager manager = (SoundManager)target;

            EditorGUILayout.Space();

            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("Preload All"))
            {
                manager.PreloadAllClips();
            }

            if (GUILayout.Button("Clear Cache"))
            {
                if (EditorUtility.DisplayDialog("Clear Audio Clip Cache", "정말로 모든 캐시된 AudioClip을 삭제하시겠습니까?", "삭제", "취소"))
                {
                    manager.ClearClipCache();
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("📁 Preloaded Clips", EditorStyles.boldLabel);

            var dict = manager.preloadAsset;
            if (manager.preloadAsset != null && manager.preloadAsset.clipPaths.Count > 0)
            {
                foreach (var pair in manager.preloadAsset.clipPaths)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.TextField(pair.key);
                    EditorGUILayout.ObjectField(pair.clip, typeof(AudioClip), false);
                    EditorGUILayout.EndHorizontal();
                }
            }
            else
            {
                EditorGUILayout.HelpBox("캐시된 AudioClip이 없습니다. 'Preload All'을 눌러서 Resources/Sounds 안의 AudioClip을 캐시하세요.", MessageType.Info);
            }
        }
    }
}
#endif
