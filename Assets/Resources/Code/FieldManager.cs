using UnityEngine;

namespace CardGame.Core
{
    public class FieldManager : MonoBehaviour
    {
        public static FieldManager Instance { get; private set; }

        [SerializeField] private Transform[] playerTopRowSlots;
        [SerializeField] private Transform[] playerBottomRowSlots;
        [SerializeField] private Transform[] opponentTopRowSlots;
        [SerializeField] private Transform[] opponentBottomRowSlots;
        [SerializeField] private GameObject Slot;
        private GameObject playerTopRow;
        private GameObject playerBottomRow;
        private GameObject opponentTopRow;
        private GameObject opponentBottomRow;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            playerTopRow = GameObject.FindWithTag("PlayerTop");
            playerBottomRow = GameObject.FindWithTag("PlayerBottom");
            opponentTopRow = GameObject.FindWithTag("OpponentTop");
            opponentBottomRow = GameObject.FindWithTag("OpponentBottom");

            if (playerTopRowSlots == null || playerTopRowSlots.Length != 5)
                playerTopRowSlots = new Transform[5];
            if (playerBottomRowSlots == null || playerBottomRowSlots.Length != 5)
                playerBottomRowSlots = new Transform[5];
            if (opponentTopRowSlots == null || opponentTopRowSlots.Length != 5)
                opponentTopRowSlots = new Transform[5];
            if (opponentBottomRowSlots == null || opponentBottomRowSlots.Length != 5)
                opponentBottomRowSlots = new Transform[5];

            for (int i = 0; i < 5; i++)
            {
                GameObject topSlot = Instantiate(Slot, playerTopRow.transform);
                topSlot.name = $"PlayerTopSlot_{i + 1}";
                playerTopRowSlots[i] = topSlot.transform;

                GameObject bottomSlot = Instantiate(Slot, playerBottomRow.transform);
                bottomSlot.name = $"PlayerBottomSlot_{i + 1}";
                playerBottomRowSlots[i] = bottomSlot.transform;

                GameObject opponentTopSlot = Instantiate(Slot, opponentTopRow.transform);
                opponentTopSlot.name = $"OpponentTopSlot_{i + 1}";
                opponentTopRowSlots[i] = opponentTopSlot.transform;

                GameObject opponentBottomSlot = Instantiate(Slot, opponentBottomRow.transform);
                opponentBottomSlot.name = $"OpponentBottomSlot_{i + 1}";
                opponentBottomRowSlots[i] = opponentBottomSlot.transform;
            }
        }

        void Start() { }

        public Transform GetSlot(string rowType, int index)
        {
            Transform[] slots = GetRowSlots(rowType);
            if (slots == null) return null;

            if (index >= 0 && index < slots.Length)
                return slots[index];

            Debug.LogError($"Index {index} is out of range for {rowType} slots.");
            return null;
        }

        /// <summary>Returns the index of the first empty slot in the given row, or -1 if full.</summary>
        public int GetFirstEmptySlotIndex(string rowType)
        {
            Transform[] slots = GetRowSlots(rowType);
            if (slots == null) return -1;

            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] != null && slots[i].childCount == 0)
                    return i;
            }
            return -1;
        }

        /// <summary>Returns true if the given row has at least one empty slot.</summary>
        public bool HasEmptySlot(string rowType)
        {
            return GetFirstEmptySlotIndex(rowType) != -1;
        }

        public void PlaceCardInSlot(GameObject card, string rowType, int index)
        {
            Transform slot = GetSlot(rowType, index);
            if (slot != null)
            {
                card.transform.SetParent(slot);
                card.transform.localPosition = Vector3.zero;
                card.transform.localRotation = Quaternion.identity;
                card.transform.localScale = Vector3.one;
            }
        }

        public void ClearSlot(string rowType, int index)
        {
            Transform slot = GetSlot(rowType, index);
            if (slot != null && slot.childCount > 0)
            {
                foreach (Transform child in slot)
                    Destroy(child.gameObject);
            }
        }

        public void ClearAllSlots()
        {
            Debug.Log("Clearing all slots...");
            ClearRow("PlayerTop");
            ClearRow("PlayerBottom");
            ClearRow("OpponentTop");
            ClearRow("OpponentBottom");
        }

        public void ClearRow(string rowType)
        {
            Transform[] slots = GetRowSlots(rowType);
            if (slots == null) return;

            foreach (Transform slot in slots)
            {
                if (slot == null) continue;
                foreach (Transform child in slot)
                    Destroy(child.gameObject);
            }
        }

        private Transform[] GetRowSlots(string rowType)
        {
            switch (rowType)
            {
                case "PlayerTop": return playerTopRowSlots;
                case "PlayerBottom": return playerBottomRowSlots;
                case "OpponentTop": return opponentTopRowSlots;
                case "OpponentBottom": return opponentBottomRowSlots;
                default:
                    Debug.LogError($"Invalid row type: {rowType}");
                    return null;
            }
        }
    }
}