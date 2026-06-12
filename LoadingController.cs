using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LoadingController : MonoBehaviour
{
    static string nextScene;
    public static bool loaded;

    [SerializeField]
    private Slider progress;

    private float currentProgress = 0f; // 슬라이더 없을 때를 위한 내부 진행도
    private const float FakeLoadDuration = 1.5f;

    public static void LoadScene(string sceneName)
    {
        nextScene = sceneName;
        Time.timeScale = 1;

        if (Application.isBatchMode ||
            (NetworkManager.Singleton != null && LanManager.IsHeadlessServer))
        {
            SceneManager.LoadScene(sceneName);
            return;
        }

        SceneManager.LoadScene("Loading");
    }

    private void Start()
    {
        StartCoroutine(SceneProcess());
    }

    IEnumerator SceneProcess()
    {
        loaded = false;
        AsyncOperation load = SceneManager.LoadSceneAsync(nextScene);
        load.allowSceneActivation = false;

        float timer = 0f;

        while (!load.isDone)
        {
            yield return null;

            float targetProgress = Mathf.Clamp01(load.progress / 0.9f);
            timer += Time.unscaledDeltaTime;

            if (progress != null)
            {
                progress.value = Mathf.Lerp(progress.value, targetProgress, 0.1f);
            }
            else
            {
                currentProgress = Mathf.Lerp(currentProgress, targetProgress, 0.1f);
            }

            bool filled = (progress != null && progress.value >= 0.99f) || 
                          (progress == null && currentProgress >= 0.99f);

            if (load.progress >= 0.9f && filled)
            {
                yield return new WaitForSecondsRealtime(FakeLoadDuration);
                loaded = true;

                // Loading 씬이 언로드되면 이 MonoBehaviour 코루틴이 중단되어 EndLoading이 호출되지 않는다.
                // GameScene 활성화 후 LoadingSceneBridge에서 EndLoading을 보낸다.
                LoadingSceneBridge.PrepareEndLoadingAfterScene(nextScene);

                load.allowSceneActivation = true;

                while (!load.isDone)
                    yield return null;

                yield break;
            }
        }
    }
}
