using UnityEngine;
using TMPro;
using UnityEngine.EventSystems;
using UnityEngine.UIElements;
using CardGame.Core;
using Unity.Netcode;
using System.Collections;

namespace CardGame.UI
{
    public class CardUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler,
                          IPointerEnterHandler, IPointerExitHandler
    {
        [Header("UI References")]
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI bodyText;
        [SerializeField] private TextMeshProUGUI manaCostText;
        [SerializeField] private TextMeshProUGUI atkDamageText;
        [SerializeField] private Image cardImage;
        [SerializeField] private GameObject cardback;

        [Header("Effect Visuals")]
        [SerializeField] private GameObject burnEffectPrefab;
        [SerializeField] private GameObject healEffectPrefab;
        [SerializeField] private GameObject shieldEffectPrefab;
        [SerializeField] private GameObject drawEffectPrefab;
        [SerializeField] private GameObject manaBoostEffectPrefab;

        [Header("Feedback")]
        [SerializeField] private TextMeshProUGUI feedbackText; // optional, assign in Inspector

        private CanvasGroup canvasGroup;
        private Transform originalParent;
        private Vector3 originalPosition;
        private Vector3 originalScale;
        private Quaternion originalRotation;
        private int originalSortingOrder;
        private Canvas canvas;

        private CardDataSO cardData;
        private int handIndex = -1;

        private const string DefaultPlayRow = "PlayerTop";

        private void Awake()
        {
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = gameObject.AddComponent<CanvasGroup>();

            canvas = GetComponent<Canvas>();
            originalScale = transform.localScale;
        }

        /// <summary>Populates the card UI with data from a <see cref="CardDataSO"/>.</summary>
        public void SetCard(CardDataSO data, int index = -1)
        {
            cardData = data;
            handIndex = index;

            if (data == null) return;

            if (nameText != null) nameText.text = data.Name;
            if (bodyText != null) bodyText.text = data.Description;
            if (manaCostText != null) manaCostText.text = data.ManaCost.ToString();
            if (atkDamageText != null) atkDamageText.text = data.Attack.ToString();
        }

        public void SetCardBackVisible(bool visible)
        {
            if (cardback != null)
                cardback.SetActive(visible);
        }

        // ?? Drag Handlers ??????????????????????????????????????????????????????

        public void OnBeginDrag(PointerEventData eventData)
        {
            var gm = Object.FindFirstObjectByType<GameManager>();
            if (gm != null && !gm.IsLocalPlayerCasting())
                return;

            originalParent = transform.parent;
            originalPosition = transform.localPosition;
            originalRotation = transform.rotation;

            canvasGroup.blocksRaycasts = false;
            transform.SetParent(transform.root);
            transform.rotation = Quaternion.identity;
        }

        public void OnDrag(PointerEventData eventData)
        {
            transform.position = eventData.position;
            transform.rotation = Quaternion.identity;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            canvasGroup.blocksRaycasts = true;

            if (TryPlayToField())
                return;

            // Snap back to hand
            transform.SetParent(originalParent);
            transform.localPosition = originalPosition;
            transform.rotation = Approximately(transform.localPosition, originalPosition)
                ? originalRotation
                : Quaternion.identity;
        }

        // ?? Hover Handlers ?????????????????????????????????????????????????????

        public void OnPointerEnter(PointerEventData eventData)
        {
            transform.localScale = originalScale * 1.15f;
            if (canvas != null)
            {
                originalSortingOrder = canvas.sortingOrder;
                canvas.overrideSorting = true;
                canvas.sortingOrder = 100;
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            transform.localScale = originalScale;
            if (canvas != null)
                canvas.sortingOrder = originalSortingOrder;
        }

        // ?? Field Placement ????????????????????????????????????????????????????

        /// <summary>
        /// Attempts to place this card onto the field.
        /// Fails fast with feedback if the player cannot afford the mana cost.
        /// </summary>
        private bool TryPlayToField()
        {
            if (cardData == null || handIndex < 0) return false;

            var gm = Object.FindFirstObjectByType<GameManager>();
            if (gm == null) return false;

            // ?? Mana check (client-side fast-fail) ????????????????????????????
            int currentMana = gm.GetLocalPlayerCurrentMana();
            if (currentMana < cardData.ManaCost)
            {
                ShowFeedback($"Not enough mana! ({currentMana}/{cardData.ManaCost})");
                return false;
            }

            // ?? Phase check ???????????????????????????????????????????????????
            if (!gm.IsLocalPlayerCasting())
                return false;

            // ?? Slot check ????????????????????????????????????????????????????
            if (!FieldManager.Instance.HasEmptySlot(DefaultPlayRow))
            {
                ShowFeedback("No empty slots on the field!");
                return false;
            }

            int slotIndex = FieldManager.Instance.GetFirstEmptySlotIndex(DefaultPlayRow);
            if (slotIndex < 0) return false;

            // All checks passed — notify server, place visually, animate
            gm.RequestPlayCardRpc(NetworkManager.Singleton.LocalClientId, handIndex);

            FieldManager.Instance.PlaceCardInSlot(gameObject, DefaultPlayRow, slotIndex);
            StartCoroutine(PlayToFieldAnimation());
            PlayEffectVFX(cardData.Effect);

            return true;
        }

        /// <summary>
        /// Briefly shows a feedback message on the card, then fades it out.
        /// Falls back to a Debug.LogWarning if no <see cref="feedbackText"/> is assigned.
        /// </summary>
        private void ShowFeedback(string message)
        {
            if (feedbackText != null)
            {
                StopCoroutine(nameof(FeedbackFadeRoutine));
                StartCoroutine(FeedbackFadeRoutine(message));
            }
            else
            {
                Debug.LogWarning($"[CardUI] {message}");
            }
        }

        private IEnumerator FeedbackFadeRoutine(string message)
        {
            feedbackText.text = message;
            feedbackText.enabled = true;
            feedbackText.color = new Color(1f, 0.3f, 0.3f, 1f); // red-ish

            yield return new WaitForSeconds(1.2f);

            float elapsed = 0f;
            float fadetime = 0.4f;
            Color start = feedbackText.color;

            while (elapsed < fadetime)
            {
                elapsed += Time.deltaTime;
                feedbackText.color = new Color(start.r, start.g, start.b, 1f - elapsed / fadetime);
                yield return null;
            }

            feedbackText.enabled = false;
        }

        /// <summary>Scales the card up then back to normal to signal it was played.</summary>
        private IEnumerator PlayToFieldAnimation()
        {
            float duration = 0.25f;
            float elapsed = 0f;
            Vector3 target = originalScale * 1.3f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                transform.localScale = Vector3.Lerp(originalScale, target, elapsed / duration);
                yield return null;
            }

            elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                transform.localScale = Vector3.Lerp(target, Vector3.one, elapsed / duration);
                yield return null;
            }

            transform.localScale = Vector3.one;
        }

        /// <summary>Spawns the correct VFX prefab for the given effect.</summary>
        private void PlayEffectVFX(CardEffect effect)
        {
            GameObject prefab = effect switch
            {
                CardEffect.Burn => burnEffectPrefab,
                CardEffect.Heal => healEffectPrefab,
                CardEffect.Shield => shieldEffectPrefab,
                CardEffect.Draw => drawEffectPrefab,
                CardEffect.ManaBoost => manaBoostEffectPrefab,
                _ => null
            };

            if (prefab == null) return;

            var vfx = Instantiate(prefab, transform.position, Quaternion.identity);
            Destroy(vfx, 2f);
        }

        // ?? Helpers ????????????????????????????????????????????????????????????

        private bool Approximately(Vector3 a, Vector3 b, float threshold = 0.01f)
        {
            return Vector3.Distance(a, b) < threshold;
        }
    }
}