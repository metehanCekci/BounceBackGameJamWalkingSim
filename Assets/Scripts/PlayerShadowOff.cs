using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

// Oyuncunun kapsülü yere gölge düşürmesin. Sahneye eklemek gerekmez, her sahne açılınca kendiliğinden çalışır.
public static class PlayerShadowOff
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        Apply();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Apply();
    }

    private static void Apply()
    {
        foreach (PlayerMovement player in Object.FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None))
            foreach (Renderer r in player.GetComponentsInChildren<Renderer>(true))
                r.shadowCastingMode = ShadowCastingMode.Off;
    }
}
