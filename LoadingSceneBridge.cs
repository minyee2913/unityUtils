using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Loading 씬이 언로드될 때 LoadingController 코루틴이 끊기므로,
/// GameScene 활성화 직후 여기서 EndLoading을 보낸다.
/// </summary>
public static class LoadingSceneBridge
{
    static bool pendingEndLoading;
    static string pendingTargetScene;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        pendingEndLoading = false;
        pendingTargetScene = null;
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    public static void PrepareEndLoadingAfterScene(string targetSceneName)
    {
        pendingTargetScene = targetSceneName;
        pendingEndLoading = true;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!pendingEndLoading || string.IsNullOrEmpty(pendingTargetScene))
            return;
        if (scene.name != pendingTargetScene)
            return;

        pendingEndLoading = false;
        pendingTargetScene = null;
        SceneManager.sceneLoaded -= OnSceneLoaded;

        TrySendEndLoading();
    }

    public static void TrySendEndLoading()
    {
        if (SceneManager.GetActiveScene().name != "GameScene")
            return;

        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsClient)
            return;

        if (NetworkSession.Local == null)
        {
            Debug.LogWarning("[LoadingSceneBridge] EndLoading deferred — NetworkSession.Local is null (retry on Session spawn)");
            return;
        }

        Debug.Log("[LoadingSceneBridge] GameScene ready — EndLoading");
        NetworkSession.Local.EndLoading();
    }
}
