using UnityEngine;

// Oyuncuya parkurda nereye koşacağını gösteren yeşil hedef işareti (mesafe yazar, hedef ekran dışındaysa kenarda ok olur).
// Adımlar sırayla gösterilir: Begin() ilkini açar, Next() bir sonrakine geçer. Inspector olaylarından çağrılır
// (ör. SirenCutscene > On Finished: Begin, EscapeCollapseCutscene > On Finished: Next).
// Cutscene sırasında (sinematik bantlar açıkken) ve ekran kararırken işaret gizlenir.
public class PathGuide : MonoBehaviour
{
    [System.Serializable]
    public class Step
    {
        public Transform target;
        public string label;                  // İşaretin altındaki yazı; mesafe her zaman yazar
        public Vector3 offset;                // Hedefin üstüne kaydırma (ör. kulenin lambası)
    }

    public Step[] steps;
    public Color color = new Color(0.25f, 1f, 0.4f);
    public float size = 22f;
    public float hideWithin = 6f;             // Bu kadar yakındayken hedef zaten görünüyor

    private WorldMarker marker;
    private int current = -1;

    public void Begin()
    {
        SetStep(0);
    }

    public void Next()
    {
        SetStep(current + 1);
    }

    public void SetStep(int index)
    {
        current = index;
        if (marker == null)
        {
            marker = gameObject.AddComponent<WorldMarker>();
            marker.showDistance = true;
            marker.pointWhenOffscreen = true; // Arkaya dönse bile ekran kenarındaki ok yolu gösterir
            marker.pointProvider = () => CurrentStep.target.position + CurrentStep.offset;
        }
        marker.color = color;
        marker.size = size;
        marker.hideWithin = hideWithin;
        if (HasStep)
            marker.label = CurrentStep.label;
        UpdateVisibility();
    }

    void LateUpdate()
    {
        if (marker != null)
            UpdateVisibility();
    }

    private bool HasStep => steps != null && current >= 0 && current < steps.Length && steps[current].target != null;

    private Step CurrentStep => steps[current];

    private void UpdateVisibility()
    {
        marker.enabled = HasStep && !CinematicBars.IsShowing && !ScreenFader.IsBusy;
    }
}
