using UnityEngine;
using TMPro;
using UnityEngine.EventSystems;
using UnityEngine.UIElements;

public class CardUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerEnterHandler, IPointerExitHandler
{
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI bodyText;
    [SerializeField] private TextMeshProUGUI manaCostText;
    [SerializeField] private TextMeshProUGUI atkDamageText;
    [SerializeField] private Image cardImage;
    [SerializeField] private GameObject cardback; 

    private CanvasGroup canvasGroup;
    private Transform originalParent;
    private Vector3 originalPosition;
    private Vector3 originalScale;
    private Quaternion originalRotation;
    private int originalSortingOrder;
    private Canvas canvas;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
        canvas = GetComponent<Canvas>();
        originalScale = transform.localScale;
    }

    /// <summary>
    /// Populates the card UI with data from a CardDataSO.
    /// </summary>
    public void SetCard(CardDataSO cardData)
    {
        if (cardData == null) return;

        if (nameText != null) nameText.text = cardData.Name;
        if (bodyText != null) bodyText.text = cardData.Description;
        if (manaCostText != null) manaCostText.text = cardData.ManaCost.ToString();
        if (atkDamageText != null) atkDamageText.text = cardData.Attack.ToString();
    }

    public void SetCardBackVisible(bool visible)
    {
         
        if (cardback != null)
        {
            cardback.SetActive(visible);
        }

         
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        originalParent = transform.parent;
        originalPosition = transform.localPosition;
        originalRotation = transform.rotation;
        canvasGroup.blocksRaycasts = false; // Allow drop targets to receive events
        transform.SetParent(transform.root); // Move to root to render above other UI
        transform.rotation = Quaternion.identity; // Reset rotation at drag start
    }

    public void OnDrag(PointerEventData eventData)
    {
        transform.position = eventData.position;
        transform.rotation = Quaternion.identity; // Keep rotation zero while dragging
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        canvasGroup.blocksRaycasts = true;
        transform.SetParent(originalParent);
        transform.localPosition = originalPosition;

        // If card is back to original position, restore original rotation
        if (Approximately(transform.localPosition, originalPosition))
        {
            transform.rotation = originalRotation;
        }
        else
        {
            transform.rotation = Quaternion.identity;
        }
        // Optionally, check for drop targets here
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        transform.localScale = originalScale * 1.15f; // Slightly enlarge
        if (canvas != null)
        {
            originalSortingOrder = canvas.sortingOrder;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 100; // Bring to front
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        transform.localScale = originalScale; // Restore scale
        if (canvas != null)
        {
            canvas.sortingOrder = originalSortingOrder;
        }
    }

    // Helper for comparing positions with a small threshold
    private bool Approximately(Vector3 a, Vector3 b, float threshold = 0.01f)
    {
        return Vector3.Distance(a, b) < threshold;
    }
}