using InfiVR;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
public class UIInteractionFeedback : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("Enabled events")]
    [SerializeField] bool hoverEnterAudio = true;
    [SerializeField] bool clickAudio = true;
    [SerializeField] bool hoverExitAudio = true;

    [Header("Override audio")]
    [SerializeField] AudioClip hoverEnterAudioClip;
    [SerializeField] AudioClip clickAudioClip;
    [SerializeField] AudioClip hoverExitAudioClip;

    public TextMeshProUGUI tooltipText;

    public AudioHandler thisAudio;

    private void OnEnable()
    {
        thisAudio = FindAnyObjectByType<AudioHandler>();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (clickAudio)
        {
            thisAudio.PlayButtonClickAudio(clickAudioClip);
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if(tooltipText != null)
        {
            tooltipText.gameObject.SetActive(true);
        }

        if (hoverEnterAudio)
        {
            thisAudio.PlayButtonHoverEnterAudio(hoverEnterAudioClip);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (tooltipText != null)
        {
            tooltipText.gameObject.SetActive(false);
        }

        if (hoverExitAudio)
        {
            thisAudio.PlayButtonHoverExitAudio(hoverExitAudioClip);
        }
    }
}
