using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Affiche des toasts en haut à droite pour chaque événement de vol.
/// S'auto-configure sur le canvas HUD existant.
/// </summary>
public class FlightNotificationSystem : MonoBehaviour
{
    public static FlightNotificationSystem Instance { get; private set; }

    private RectTransform _container;
    private readonly Queue<string> _queue = new();
    private bool _showing;

    private const float ToastWidth   = 340f;
    private const float ToastHeight  = 52f;
    private const float ShowDuration = 2.8f;
    private const float SlideDur     = 0.28f;
    private const float FadeDur      = 0.35f;
    private const int   MaxVisible   = 4;

    private readonly List<RectTransform> _visible = new();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private IEnumerator Start()
    {
        yield return null; // attendre que le HUD canvas existe

        Canvas hud = null;
        foreach (var c in FindObjectsByType<Canvas>())
            if (c.renderMode == RenderMode.ScreenSpaceOverlay) { hud = c; break; }
        if (hud == null) yield break;

        // Conteneur vertical ancré en haut à droite
        var go = new GameObject("ToastContainer");
        go.transform.SetParent(hud.transform, false);

        _container = go.AddComponent<RectTransform>();
        _container.anchorMin        = new Vector2(1f, 1f);
        _container.anchorMax        = new Vector2(1f, 1f);
        _container.pivot            = new Vector2(1f, 1f);
        _container.anchoredPosition = new Vector2(-8f, -70f);
        _container.sizeDelta        = new Vector2(ToastWidth, 0f);

        var layout = go.AddComponent<VerticalLayoutGroup>();
        layout.spacing            = 6f;
        layout.childAlignment     = TextAnchor.UpperRight;
        layout.childControlWidth  = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth  = true;
        layout.childForceExpandHeight = false;
        layout.reverseArrangement = false;
    }

    public void Show(string message)
    {
        _queue.Enqueue(message);
        if (!_showing) StartCoroutine(ProcessQueue());
    }

    private IEnumerator ProcessQueue()
    {
        _showing = true;
        while (_queue.Count > 0)
        {
            if (_visible.Count >= MaxVisible)
                yield return new WaitUntil(() => _visible.Count < MaxVisible);

            SpawnToast(_queue.Dequeue());
            yield return new WaitForSeconds(0.18f); // léger décalage entre toasts
        }
        _showing = false;
    }

    private void SpawnToast(string message)
    {
        if (_container == null) return;

        // Fond
        var go  = new GameObject("Toast");
        go.transform.SetParent(_container, false);

        var rect = go.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(ToastWidth, ToastHeight);

        var img   = go.AddComponent<Image>();
        img.color = new Color(0.08f, 0.08f, 0.12f, 0.92f);

        // Bande colorée gauche
        var stripe   = new GameObject("Stripe");
        stripe.transform.SetParent(go.transform, false);
        var stripeRect = stripe.AddComponent<RectTransform>();
        stripeRect.anchorMin = Vector2.zero;
        stripeRect.anchorMax = new Vector2(0f, 1f);
        stripeRect.offsetMin = Vector2.zero;
        stripeRect.offsetMax = new Vector2(4f, 0f);
        var stripeImg   = stripe.AddComponent<Image>();
        stripeImg.color = new Color(0.3f, 0.75f, 1f);

        // Texte
        var txtGo = new GameObject("Text");
        txtGo.transform.SetParent(go.transform, false);
        var txtRect = txtGo.AddComponent<RectTransform>();
        txtRect.anchorMin = Vector2.zero;
        txtRect.anchorMax = Vector2.one;
        txtRect.offsetMin = new Vector2(12f, 4f);
        txtRect.offsetMax = new Vector2(-8f, -4f);

        var tmp       = txtGo.AddComponent<TextMeshProUGUI>();
        tmp.text      = message;
        tmp.fontSize  = 13f;
        tmp.color     = Color.white;
        tmp.alignment = TextAlignmentOptions.MidlineLeft;

        // Animation : glisse depuis la droite, attend, fond
        var canvasGroup = go.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;

        _visible.Add(rect);

        // Slide in + fade in
        rect.anchoredPosition = new Vector2(ToastWidth, 0f);
        rect.DOAnchorPosX(0f, SlideDur).SetEase(Ease.OutCubic);
        canvasGroup.DOFade(1f, SlideDur);

        // Attente puis fade out + destroy
        DOVirtual.DelayedCall(ShowDuration, () =>
        {
            canvasGroup.DOFade(0f, FadeDur).OnComplete(() =>
            {
                _visible.Remove(rect);
                Destroy(go);
            });
        });
    }
}
