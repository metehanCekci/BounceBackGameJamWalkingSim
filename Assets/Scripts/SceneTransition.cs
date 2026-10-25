using System.Collections;
using UnityEngine;

// Inspector olaylarından karartma / sahne geçişi çağırmak için (ör. görev bitince).
// Olaya bu objeyi sürükleyip LoadNextScene, FadeOut ya da FadeIn seç.
public class SceneTransition : MonoBehaviour
{
    public bool loadScene = true;          // Kapalıysa sadece siyaha kararır
    [Tooltip("Build listesindeki sıra. -1: bu sahneden sonraki sahne.")]
    public int nextSceneIndex = ScreenFader.NextScene;
    public float fadeDuration = 1.5f;
    public bool waitForSubtitles = true;   // Önce o an konuşulan bitsin

    public void LoadNextScene()
    {
        StartCoroutine(Go());
    }

    public void FadeOut()
    {
        ScreenFader.Instance.FadeOut(fadeDuration);
    }

    public void FadeIn()
    {
        ScreenFader.Instance.FadeIn(fadeDuration);
    }

    private IEnumerator Go()
    {
        if (waitForSubtitles)
        {
            // Aynı karede kuyruğa eklenen satırların başlaması için bir kare bekle
            yield return null;
            while (Subtitles.IsPlaying)
                yield return null;
        }

        if (loadScene)
            ScreenFader.Instance.TransitionToScene(nextSceneIndex, fadeDuration, fadeDuration);
        else
            ScreenFader.Instance.FadeOut(fadeDuration);
    }
}
