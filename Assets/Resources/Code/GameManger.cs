using CardGame.Core;
using CardGame.UI;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace CardGame.Core
{
    public class GameManager : NetworkBehaviour
    {
        [SerializeField] private Transform playersHandPanel;
        [SerializeField] private Transform opponentsHandPanel;
        [SerializeField] private Transform yourDeckPanel;
        [SerializeField] private Transform opponentsDeckPanel;
        [SerializeField] private GameObject cardPrefab;
        [SerializeField] private float fanSpread = 10f;
        [SerializeField] private float fanRadius = 8f;


        [SerializeField] private Transform[] playerTopRowSlots;
        [SerializeField] private Transform[] playerBottomRowSlots;
        [SerializeField] private Transform[] opponentTopRowSlots;
        [SerializeField] private Transform[] opponentBottomRowSlots;

        [Header("Player Stats UI")]
        [SerializeField] private PlayerStatsUI playerStatsUI;

        private List<Player> players = new List<Player>();
        private Dictionary<ulong, int> clientPlayerIndices = new Dictionary<ulong, int>();
        private NetworkVariable<int> currentPlayerIndex = new NetworkVariable<int>();
        private NetworkVariable<bool> gameEnded = new NetworkVariable<bool>();

        // Cache all cards for lookup by name
        private Dictionary<string, CardDataSO> cardLookup;
        private DeckManager deckManager;
        private UIManager uiManager;

        private void Awake()
        {
            Debug.Log("GameManager Awake: Loading card data...");
            cardLookup = new Dictionary<string, CardDataSO>();
            var allCards = Resources.LoadAll<CardDataSO>("Cards");
            foreach (var card in allCards)
            {
                cardLookup[card.Name] = card;
            }
            deckManager = new DeckManager(cardLookup);
            uiManager = new UIManager(playersHandPanel, opponentsHandPanel, yourDeckPanel, opponentsDeckPanel, cardPrefab, fanSpread, fanRadius, playerStatsUI);

            // Auto-find field slots by tag
            playerTopRowSlots = FindSlotsByTag("PlayerTopRowSlot");
            playerBottomRowSlots = FindSlotsByTag("PlayerBottomRowSlot");
            opponentTopRowSlots = FindSlotsByTag("OpponentTopRowSlot");
            opponentBottomRowSlots = FindSlotsByTag("OpponentBottomRowSlot");
        }

        private Transform[] FindSlotsByTag(string tag)
        {
            var slots = GameObject.FindGameObjectsWithTag(tag);
            if (slots == null || slots.Length == 0)
            {
                Debug.LogWarning($"No slots found with tag: {tag}");
                return new Transform[0];
            }
            // Order by name for consistent slot assignment
            return slots.OrderBy(go => go.name).Select(go => go.transform).ToArray();
        }

        private void ClearFieldSlots()
        {
            // Clear all player and opponent slots
            ClearSlots(playerTopRowSlots);
            ClearSlots(playerBottomRowSlots);
            ClearSlots(opponentTopRowSlots);
            ClearSlots(opponentBottomRowSlots);
        }

        private void ClearSlots(Transform[] slots)
        {
            if (slots == null) return;
            foreach (var slot in slots)
            {
                if (slot == null) continue;
                foreach (Transform child in slot)
                {
                    Destroy(child.gameObject);
                }
            }
        }

        public override void OnNetworkSpawn()
        {
            Debug.Log("GameManager OnNetworkSpawn: IsServer=" + IsServer);
            PositionPanels();
            if (IsServer)
            {
                NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;

                if (clientPlayerIndices.Count == 0)
                {
                    OnClientConnected(NetworkManager.Singleton.LocalClientId);
                }
            }

            if (IsClient)
            {
                NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
            }

            if (IsHost)
            {
                NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
            }
        }

        private void PositionPanels()
        {
            // Example positions for anchored UI (adjust as needed)
            float playerHandY = 100f;      // Bottom of screen
            float opponentHandY = -50f;    // Top of screen
            float decktOffset1 = 260f;
            float decktOffset2 = 300f;
            float deckSacle = 1.2f;

            // Set positions for both panels using RectTransform
            if (playersHandPanel != null && playersHandPanel is RectTransform playerRect)
            {
                playerRect.anchoredPosition = new Vector2(0, playerHandY);
                playerRect.anchorMin = new Vector2(0.5f, 0f); // Center bottom
                playerRect.anchorMax = new Vector2(0.5f, 0f);
                playerRect.pivot = new Vector2(0.5f, 0f);
            }
            if (opponentsHandPanel != null && opponentsHandPanel is RectTransform oppRect)
            {
                oppRect.anchoredPosition = new Vector2(0, opponentHandY);
                oppRect.anchorMin = new Vector2(0.5f, 1f); // Center top
                oppRect.anchorMax = new Vector2(0.5f, 1f);
                oppRect.pivot = new Vector2(0.5f, 1f);
            }
            if (yourDeckPanel != null && yourDeckPanel is RectTransform yourDeckRect)
            {
                yourDeckRect.anchoredPosition = new Vector2(-decktOffset1, decktOffset2);
                yourDeckRect.anchorMin = new Vector2(1f, 0f); // Center bottom
                yourDeckRect.anchorMax = new Vector2(1f, 0f);
                yourDeckRect.pivot = new Vector2(1f, 0f);
                yourDeckRect.localScale = Vector3.one * deckSacle;
            }
            if (opponentsDeckPanel != null && opponentsDeckPanel is RectTransform oppDeckRect)
            {
                oppDeckRect.anchoredPosition = new Vector2(decktOffset1, -decktOffset2);
                oppDeckRect.anchorMin = new Vector2(0f, 1f); // Center top
                oppDeckRect.anchorMax = new Vector2(0f, 1f);
                oppDeckRect.pivot = new Vector2(0f, 1f);
                oppDeckRect.localScale = Vector3.one * deckSacle;
            }
        }

     


        private void OnClientConnected(ulong clientId)
        {
            Debug.Log($"Client connected: {clientId}");
            ClearFieldSlots();
            int assignedIndex;
            if (clientId == NetworkManager.Singleton.LocalClientId && IsServer)
            {
                assignedIndex = 0; // Host is always player 1
            }
            else
            {
                assignedIndex = 1; // Client is always player 2
            }
            clientPlayerIndices[clientId] = assignedIndex;

            var deck = deckManager.CreateDeck();
            var player = new Player(deck);
            if (players.Count <= assignedIndex)
                players.Add(player);
            else
                players[assignedIndex] = player;

            deckManager.ShuffleDeck(player.Deck);
            deckManager.DrawStartingHand(player, 5);

            UpdateHandClientRpc(deckManager.GetCardIds(player.Hand), assignedIndex);

            if (players.Count == 2)
            {
                UpdateDeckClientRpc(deckManager.GetCardIds(players[0].Deck), 0);
                UpdateDeckClientRpc(deckManager.GetCardIds(players[1].Deck), 1);

                // Synchronize stats for all clients
                var p0 = players[0];
                var p1 = players[1];
                UpdatePlayerStatsClientRpc(
                    p0.Hp, p0.CurrentMana, p0.MaxMana,
                    p1.Hp, p1.CurrentMana, p1.MaxMana
                );
            }

            if (clientPlayerIndices.Count == 2)
            {
                currentPlayerIndex.Value = Random.Range(0, 2);
                gameEnded.Value = false;
                Debug.Log($"Player {currentPlayerIndex.Value + 1} goes first.");
                for (int i = 0; i < players.Count; i++)
                    UpdateHandClientRpc(deckManager.GetCardIds(players[i].Hand), i);
            }
        }

        private void OnClientDisconnected(ulong clientId)
        {
            Debug.Log($"Client disconnected: {clientId}");
            if (clientPlayerIndices.TryGetValue(clientId, out int index))
            {
                clientPlayerIndices.Remove(clientId);
                if (index < players.Count)
                    players.RemoveAt(index);
            }

            if (players.Count == 2)
            {
                var p0 = players[0];
                var p1 = players[1];
                UpdatePlayerStatsClientRpc(
                    p0.Hp, p0.CurrentMana, p0.MaxMana,
                    p1.Hp, p1.CurrentMana, p1.MaxMana
                );
            }
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void RequestDrawCardRpc(ulong clientId)
        {
            if (!clientPlayerIndices.TryGetValue(clientId, out int playerIndex)) return;
            var player = players[playerIndex];
            if (player.Deck.Count == 0)
            {
                gameEnded.Value = true;
                EndGameClientRpc(playerIndex);
                return;
            }
            deckManager.DrawCard(player);
            UpdateHandClientRpc(deckManager.GetCardIds(player.Hand), playerIndex);

            // Update deck visuals after draw
            UpdateDeckClientRpc(deckManager.GetCardIds(player.Deck), playerIndex);

            if (players.Count == 2)
            {
                var p0 = players[0];
                var p1 = players[1];
                UpdatePlayerStatsClientRpc(
                    p0.Hp, p0.CurrentMana, p0.MaxMana,
                    p1.Hp, p1.CurrentMana, p1.MaxMana
                );
            }
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void RequestPlayCardRpc(ulong clientId, int handIndex)
        {
            if (!clientPlayerIndices.TryGetValue(clientId, out int playerIndex)) return;
            var player = players[playerIndex];
            if (handIndex < 0 || handIndex >= player.Hand.Count) return;
            var card = player.Hand[handIndex];
            player.Hand.RemoveAt(handIndex);
            player.Field.Add(card);
            UpdateHandClientRpc(deckManager.GetCardIds(player.Hand), playerIndex);
            UpdateFieldClientRpc(deckManager.GetCardIds(player.Field), playerIndex);

            // Update deck visuals after play
            UpdateDeckClientRpc(deckManager.GetCardIds(player.Deck), playerIndex);

            if (players.Count == 2)
            {
                var p0 = players[0];
                var p1 = players[1];
                UpdatePlayerStatsClientRpc(
                    p0.Hp, p0.CurrentMana, p0.MaxMana,
                    p1.Hp, p1.CurrentMana, p1.MaxMana
                );
            }
        }

        [ClientRpc]
        private void UpdateHandClientRpc(FixedString64Bytes[] handCardIds, int playerIndex)
        {
            Debug.Log($"UpdateHandClientRpc: LocalClientId={NetworkManager.Singleton.LocalClientId}, playerIndex={playerIndex}, LocalPlayerIndex={GetLocalPlayerIndex()}");
            var handCards = new List<CardDataSO>();
            foreach (var cardId in handCardIds)
            {
                if (cardLookup.TryGetValue(cardId.ToString(), out var cardData))
                    handCards.Add(cardData);
            }

            if (GetLocalPlayerIndex() == playerIndex)
            {
                // Show your own hand (face up)
                uiManager.ShowHandUI(new Player(new List<CardDataSO>()), playersHandPanel, handCards, false);
            }
            else
            {
                // Show opponent's hand (card backs only)
                uiManager.ShowHandUI(new Player(new List<CardDataSO>()), opponentsHandPanel, handCards, true);
            }

            if (players.Count == 2)
            {
                var p0 = players[0];
                var p1 = players[1];
                UpdatePlayerStatsClientRpc(
                    p0.Hp, p0.CurrentMana, p0.MaxMana,
                    p1.Hp, p1.CurrentMana, p1.MaxMana
                );
            }
        }

       
        [ClientRpc]
        private void UpdateFieldClientRpc(FixedString64Bytes[] fieldCardIds, int playerIndex)
        {
            // Example: Assume first 5 cards are top row, next 5 are bottom row
            var topRow = fieldCardIds.Take(5).ToArray();
            var bottomRow = fieldCardIds.Skip(5).Take(5).ToArray();

            Transform[] topSlots = playerIndex == GetLocalPlayerIndex() ? playerTopRowSlots : opponentTopRowSlots;
            Transform[] bottomSlots = playerIndex == GetLocalPlayerIndex() ? playerBottomRowSlots : opponentBottomRowSlots;

            for (int i = 0; i < topSlots.Length; i++)
            {
                // Clear slot, then instantiate card if exists
                foreach (Transform child in topSlots[i]) Destroy(child.gameObject);
                if (i < topRow.Length && cardLookup.TryGetValue(topRow[i].ToString(), out var cardData))
                {
                    var cardObj = Instantiate(cardPrefab, topSlots[i]);
                    // Optionally set card visuals here
                }
            }
            for (int i = 0; i < bottomSlots.Length; i++)
            {
                foreach (Transform child in bottomSlots[i]) Destroy(child.gameObject);
                if (i < bottomRow.Length && cardLookup.TryGetValue(bottomRow[i].ToString(), out var cardData))
                {
                    var cardObj = Instantiate(cardPrefab, bottomSlots[i]);
                    // Optionally set card visuals here
                }
            }
        }

        [ClientRpc]
        private void EndGameClientRpc(int losingPlayerIndex)
        {
            if (GetLocalPlayerIndex() == losingPlayerIndex)
            {
                Debug.Log("You lost the game!");
            }
            else
            {
                Debug.Log("You won the game!");
            }

        }

        [ClientRpc]
        private void UpdatePlayerStatsClientRpc(
        int playerHp, int playerMana, int playerMaxMana,
        int opponentHp, int opponentMana, int opponentMaxMana)
        {
            if (playerStatsUI == null)
                return;

            playerStatsUI.UpdateStats(
                playerHp, playerMana, playerMaxMana,
                opponentHp, opponentMana, opponentMaxMana
            );
        }

        private int GetLocalPlayerIndex()
        {
            var localId = NetworkManager.Singleton.LocalClientId;
            return clientPlayerIndices.ContainsKey(localId) ? clientPlayerIndices[localId] : -1;
        }

        [ClientRpc]
        private void UpdateDeckClientRpc(FixedString64Bytes[] deckCardIds, int playerIndex)
        {
            var deckCards = new List<CardDataSO>();
            foreach (var cardId in deckCardIds)
            {
                if (cardLookup.TryGetValue(cardId.ToString(), out var cardData))
                    deckCards.Add(cardData);
            }

            if (GetLocalPlayerIndex() == playerIndex)
            {
                uiManager.ShowDeckUI(deckCards, yourDeckPanel, true);
            }
            else
            {
                uiManager.ShowDeckUI(deckCards, opponentsDeckPanel, true);
            }
        }
    }
}