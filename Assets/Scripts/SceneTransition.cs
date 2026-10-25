using System.Collections;
using UnityEngine;

// Inspector olaylarından karartma / sahne geçişi çağırmak için (ör. görev bitince).
// Olaya bu objeyi sürükleyip LoadNextScene, FadeOut ya da FadeIn seç.
public class SceneTransition : MonoBehaviour
{
    public string nextScene;               // Boşsa sadece siyaha kararır
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

        if (string.IsNullOrEmpty(nextScene))
            ScreenFader.Instance.FadeOut(fadeDuration);
        else
            ScreenFader.Instance.TransitionToScene(nextScene, fadeDuration, fadeDuration);
    }
}
