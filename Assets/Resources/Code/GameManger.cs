using CardGame.Core;
using CardGame.UI;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace CardGame.Core
{
    public enum TurnPhase
    {
        ManaGain,
        Draw,
        Casting,
        Resolution,
        End
    }

    public class GameManager : NetworkBehaviour
    {
        [SerializeField] private Transform playersHandPanel;
        [SerializeField] private Transform opponentsHandPanel;
        [SerializeField] private Transform yourDeckPanel;
        [SerializeField] private Transform opponentsDeckPanel;
        [SerializeField] private GameObject cardPrefab;
        [SerializeField] private float fanSpread = 10f;
        [SerializeField] private float fanRadius = 8f;

        [Header("Player Stats UI")]
        [SerializeField] private PlayerStatsUI playerStatsUI;

        [Header("Turn UI")]
        [SerializeField] private GameObject endTurnButton;

        private List<Player> players = new List<Player>();
        private Dictionary<ulong, int> clientPlayerIndices = new Dictionary<ulong, int>();

        private NetworkVariable<int> currentPlayerIndex = new NetworkVariable<int>();
        private NetworkVariable<bool> gameEnded = new NetworkVariable<bool>();
        private NetworkVariable<int> currentPhaseNet = new NetworkVariable<int>((int)TurnPhase.ManaGain);

        private Dictionary<string, CardDataSO> cardLookup;
        private DeckManager deckManager;
        private UIManager uiManager;
        private TurnManager turnManager;

        private TurnPhase CurrentPhase => (TurnPhase)currentPhaseNet.Value;

        private void OnValidate()
        {
            if (uiManager == null) return;
            RefreshAllHands();
        }

        private void RefreshAllHands()
        {
            if (players == null || players.Count == 0) return;

            int localIndex = GetLocalPlayerIndex();

            for (int i = 0; i < players.Count; i++)
            {
                bool isLocal = localIndex == i;
                var panel = isLocal ? playersHandPanel : opponentsHandPanel;
                var handCards = new List<CardDataSO>();

                foreach (var card in players[i].Hand)
                    if (cardLookup.TryGetValue(card.Name, out var cardData))
                        handCards.Add(cardData);

                uiManager.ShowHandUI(players[i], panel, handCards, !isLocal);
            }
        }

        private void Awake()
        {
            cardLookup = new Dictionary<string, CardDataSO>();
            var allCards = Resources.LoadAll<CardDataSO>("Cards");
            foreach (var card in allCards)
                cardLookup[card.Name] = card;

            deckManager = new DeckManager(cardLookup);
            uiManager = new UIManager(
                playersHandPanel, opponentsHandPanel,
                yourDeckPanel, opponentsDeckPanel,
                cardPrefab, fanSpread, fanRadius, playerStatsUI
            );

            turnManager = TurnManager.Instance;
        }

        public override void OnNetworkSpawn()
        {
            PositionPanels();

            if (IsServer)
            {
                NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;

                if (clientPlayerIndices.Count == 0)
                    OnClientConnected(NetworkManager.Singleton.LocalClientId);
            }

            if (endTurnButton != null)
            {
                endTurnButton.GetComponent<UnityEngine.UI.Button>()
                    .onClick.AddListener(OnEndTurnButtonPressed);
            }

            currentPhaseNet.OnValueChanged += OnPhaseChanged;
            currentPlayerIndex.OnValueChanged += OnCurrentPlayerChanged;
        }

        public override void OnNetworkDespawn()
        {
            currentPhaseNet.OnValueChanged -= OnPhaseChanged;
            currentPlayerIndex.OnValueChanged -= OnCurrentPlayerChanged;
        }

        // ?? Panel Positioning ??????????????????????????????????????????????????

        private void PositionPanels()
        {
            const float playerHandY = 100f;
            const float opponentHandY = -50f;
            const float deckOffset1 = 260f;
            const float deckOffset2 = 300f;
            const float deckScale = 1.2f;

            if (playersHandPanel is RectTransform playerRect)
            {
                playerRect.anchoredPosition = new Vector2(0, playerHandY);
                playerRect.anchorMin = new Vector2(0.5f, 0f);
                playerRect.anchorMax = new Vector2(0.5f, 0f);
                playerRect.pivot = new Vector2(0.5f, 0f);
            }
            if (opponentsHandPanel is RectTransform oppRect)
            {
                oppRect.anchoredPosition = new Vector2(0, opponentHandY);
                oppRect.anchorMin = new Vector2(0.5f, 1f);
                oppRect.anchorMax = new Vector2(0.5f, 1f);
                oppRect.pivot = new Vector2(0.5f, 1f);
            }
            if (yourDeckPanel is RectTransform yourDeckRect)
            {
                yourDeckRect.anchoredPosition = new Vector2(-deckOffset1, deckOffset2);
                yourDeckRect.anchorMin = new Vector2(1f, 0f);
                yourDeckRect.anchorMax = new Vector2(1f, 0f);
                yourDeckRect.pivot = new Vector2(1f, 0f);
                yourDeckRect.localScale = Vector3.one * deckScale;
            }
            if (opponentsDeckPanel is RectTransform oppDeckRect)
            {
                oppDeckRect.anchoredPosition = new Vector2(deckOffset1, -deckOffset2);
                oppDeckRect.anchorMin = new Vector2(0f, 1f);
                oppDeckRect.anchorMax = new Vector2(0f, 1f);
                oppDeckRect.pivot = new Vector2(0f, 1f);
                oppDeckRect.localScale = Vector3.one * deckScale;
            }
        }

        // ?? Connection Handling ????????????????????????????????????????????????

        private void OnClientConnected(ulong clientId)
        {
            Debug.Log($"Client connected: {clientId}");

            int assignedIndex = clientPlayerIndices.Count;
            if (assignedIndex > 1)
            {
                Debug.LogWarning($"More than 2 clients tried to connect. Ignoring {clientId}.");
                return;
            }

            clientPlayerIndices[clientId] = assignedIndex;

            SetLocalPlayerIndexClientRpc(assignedIndex, new ClientRpcParams
            {
                Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } }
            });

            ClearAllSlotsClientRpc();

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
                SyncDecksAndStats();

                currentPlayerIndex.Value = Random.Range(0, 2);
                gameEnded.Value = false;
                Debug.Log($"Player {currentPlayerIndex.Value + 1} goes first.");

                for (int i = 0; i < players.Count; i++)
                    UpdateHandClientRpc(deckManager.GetCardIds(players[i].Hand), i);

                StartGame();
            }
        }

        [ClientRpc]
        private void SetLocalPlayerIndexClientRpc(int playerIndex, ClientRpcParams clientRpcParams = default)
        {
            var localId = NetworkManager.Singleton.LocalClientId;
            clientPlayerIndices[localId] = playerIndex;
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
                SyncDecksAndStats();
        }

        // ?? Game Startup ???????????????????????????????????????????????????????

        private void StartGame()
        {
            if (turnManager == null)
                turnManager = TurnManager.Instance;

            turnManager.Initialize(players.Count);

            turnManager.OnTurnStart += HandleTurnStartServer;
            turnManager.OnManaGainPhase += ManaGainPhase;
            turnManager.OnDrawPhase += DrawPhase;
            turnManager.OnCastingPhase += CastingPhase;
            turnManager.OnResolutionPhase += ResolutionPhase;
            turnManager.OnEndPhase += EndPhase;

            turnManager.StartTurn();

            NotifyTurnStartClientRpc(currentPlayerIndex.Value, (int)TurnPhase.ManaGain);
        }

        // ?? Turn / Phase Callbacks (Server Only) ???????????????????????????????

        private void HandleTurnStartServer(int playerIndex)
        {
            currentPlayerIndex.Value = playerIndex;
            currentPhaseNet.Value = (int)TurnPhase.ManaGain;
            Debug.Log($"[Server] Turn Start: Player {playerIndex + 1}");
        }

        // ?? NetworkVariable Callbacks (All Clients) ????????????????????????????

        private void OnCurrentPlayerChanged(int previous, int current)
        {
            RefreshTurnButtonUI();
        }

        private void OnPhaseChanged(int previous, int current)
        {
            RefreshTurnButtonUI();
        }

        // ?? End Turn Button ????????????????????????????????????????????????????

        public void OnEndTurnButtonPressed()
        {
            int localPlayerIndex = GetLocalPlayerIndex();
            if (localPlayerIndex != currentPlayerIndex.Value || gameEnded.Value)
                return;

            RequestAdvancePhaseRpc(NetworkManager.Singleton.LocalClientId);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void RequestAdvancePhaseRpc(ulong clientId)
        {
            if (!clientPlayerIndices.TryGetValue(clientId, out int playerIndex)) return;
            if (playerIndex != currentPlayerIndex.Value) return;

            TurnPhase phase = CurrentPhase;

            switch (phase)
            {
                case TurnPhase.ManaGain:
                    ManaGainPhase(playerIndex);
                    currentPhaseNet.Value = (int)TurnPhase.Draw;
                    break;
                case TurnPhase.Draw:
                    DrawPhase(playerIndex);
                    currentPhaseNet.Value = (int)TurnPhase.Casting;
                    break;
                case TurnPhase.Casting:
                    CastingPhase(playerIndex);
                    currentPhaseNet.Value = (int)TurnPhase.Resolution;
                    break;
                case TurnPhase.Resolution:
                    ResolutionPhase(playerIndex);
                    currentPhaseNet.Value = (int)TurnPhase.End;
                    break;
                case TurnPhase.End:
                    EndPhase(playerIndex);
                    turnManager.EndTurn();
                    break;
            }
        }

        private void RefreshTurnButtonUI()
        {
            if (endTurnButton == null) return;

            bool isMyTurn = GetLocalPlayerIndex() == currentPlayerIndex.Value && !gameEnded.Value;
            endTurnButton.SetActive(true);

            var button = endTurnButton.GetComponent<UnityEngine.UI.Button>();
            var buttonText = endTurnButton.GetComponentInChildren<TMPro.TextMeshProUGUI>();

            if (button != null)
                button.interactable = isMyTurn;

            if (buttonText != null)
            {
                buttonText.text = isMyTurn
                    ? PhaseActionLabel(CurrentPhase)
                    : "Not Your Turn";
            }
        }

        private static string PhaseActionLabel(TurnPhase phase) => phase switch
        {
            TurnPhase.ManaGain => "Gain Mana",
            TurnPhase.Draw => "Draw Card",
            TurnPhase.Casting => "End Casting",
            TurnPhase.Resolution => "Resolve",
            TurnPhase.End => "End Turn",
            _ => "Next Phase"
        };

        [ClientRpc]
        private void NotifyTurnStartClientRpc(int playerIndex, int phase)
        {
            Debug.Log($"[Client] Turn started for Player {playerIndex + 1}, Phase: {(TurnPhase)phase}");
            RefreshTurnButtonUI();
        }

        // ?? Card RPCs ??????????????????????????????????????????????????????????

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
            UpdateDeckClientRpc(deckManager.GetCardIds(player.Deck), playerIndex);
            SyncDecksAndStats();
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void RequestPlayCardRpc(ulong clientId, int handIndex)
        {
            if (!clientPlayerIndices.TryGetValue(clientId, out int playerIndex)) return;

            var player = players[playerIndex];
            if (handIndex < 0 || handIndex >= player.Hand.Count) return;

            var card = player.Hand[handIndex];

            // Mana check
            if (player.CurrentMana < card.ManaCost)
            {
                Debug.LogWarning($"[Server] Player {playerIndex + 1} cannot afford {card.Name} (cost {card.ManaCost}, mana {player.CurrentMana}).");
                return;
            }

            // Deduct mana
            player.CurrentMana -= card.ManaCost;

            // Move card from hand to field
            player.Hand.RemoveAt(handIndex);
            player.Field.Add(card);

            // Resolve effect on the server
            int opponentIndex = (playerIndex + 1) % players.Count;
            var opponent = players[opponentIndex];
            var result = CardEffectHandler.Apply(card, player, opponent);

            // If the effect caused the opponent to reach 0 HP, end the game
            if (opponent.Hp <= 0)
            {
                gameEnded.Value = true;
                EndGameClientRpc(opponentIndex);
            }

            // If Draw effect, draw extra cards now
            if (result.Effect == CardEffect.Draw && result.Value > 0)
            {
                for (int i = 0; i < result.Value && player.Deck.Count > 0; i++)
                    deckManager.DrawCard(player);
            }

            // Notify clients: update hand, field, deck, and stats
            UpdateHandClientRpc(deckManager.GetCardIds(player.Hand), playerIndex);
            UpdateFieldClientRpc(deckManager.GetCardIds(player.Field), playerIndex);
            UpdateDeckClientRpc(deckManager.GetCardIds(player.Deck), playerIndex);

            // Broadcast the effect so clients can play VFX
            NotifyCardPlayedClientRpc(playerIndex, card.Name, (int)result.Effect, result.Value);

            SyncDecksAndStats();
        }

        // ?? ClientRpcs ?????????????????????????????????????????????????????????

        [ClientRpc]
        private void UpdateHandClientRpc(FixedString64Bytes[] handCardIds, int playerIndex)
        {
            var handCards = new List<CardDataSO>();
            foreach (var cardId in handCardIds)
                if (cardLookup.TryGetValue(cardId.ToString(), out var cardData))
                    handCards.Add(cardData);

            bool isLocal = GetLocalPlayerIndex() == playerIndex;
            var panel = isLocal ? playersHandPanel : opponentsHandPanel;
            uiManager.ShowHandUI(new Player(new List<CardDataSO>()), panel, handCards, !isLocal);
        }

        [ClientRpc]
        private void UpdateFieldClientRpc(FixedString64Bytes[] fieldCardIds, int playerIndex)
        {
            // Field visual update — cards are already placed by CardUI.TryPlayToField on the local client.
            // For the remote client, instantiate cards into opponent field slots.
            bool isLocal = GetLocalPlayerIndex() == playerIndex;
            if (isLocal) return; // Local player's field is managed by CardUI drag-and-drop

            string rowType = "OpponentBottom";
            FieldManager.Instance.ClearRow(rowType);

            int slotIndex = 0;
            foreach (var cardId in fieldCardIds)
            {
                if (!cardLookup.TryGetValue(cardId.ToString(), out var cardData)) continue;
                if (slotIndex >= 5) break;

                var cardGo = Instantiate(cardPrefab);
                var cardUi = cardGo.GetComponent<CardUI>();
                if (cardUi != null)
                {
                    cardUi.SetCard(cardData);
                    cardUi.SetCardBackVisible(true); // Face-down for opponent cards
                }

                FieldManager.Instance.PlaceCardInSlot(cardGo, rowType, slotIndex);
                slotIndex++;
            }
        }

        [ClientRpc]
        private void UpdateDeckClientRpc(FixedString64Bytes[] deckCardIds, int playerIndex)
        {
            var deckCards = new List<CardDataSO>();
            foreach (var cardId in deckCardIds)
                if (cardLookup.TryGetValue(cardId.ToString(), out var cardData))
                    deckCards.Add(cardData);

            var panel = GetLocalPlayerIndex() == playerIndex ? yourDeckPanel : opponentsDeckPanel;
            uiManager.ShowDeckUI(deckCards, panel, true);
        }

        [ClientRpc]
        private void UpdatePlayerStatsClientRpc(
            int playerHp, int playerMana, int playerMaxMana,
            int opponentHp, int opponentMana, int opponentMaxMana)
        {
            if (playerStatsUI == null) return;

            int localPlayerIndex = GetLocalPlayerIndex();
            if (localPlayerIndex == 0)
                playerStatsUI.UpdateStats(playerHp, playerMana, playerMaxMana, opponentHp, opponentMana, opponentMaxMana);
            else if (localPlayerIndex == 1)
                playerStatsUI.UpdateStats(opponentHp, opponentMana, opponentMaxMana, playerHp, playerMana, playerMaxMana);
        }

        /// <summary>
        /// Tells all clients a card was played and what effect fired, so they can trigger VFX.
        /// </summary>
        [ClientRpc]
        private void NotifyCardPlayedClientRpc(int playerIndex, FixedString64Bytes cardName, int effectInt, int effectValue)
        {
            var effect = (CardEffect)effectInt;
            bool isLocal = GetLocalPlayerIndex() == playerIndex;

            Debug.Log($"[Client] {(isLocal ? "You" : "Opponent")} played '{cardName}' — Effect: {effect} ({effectValue})");

            // Additional client-side VFX/audio hooks can be added here
        }

        [ClientRpc]
        private void EndGameClientRpc(int losingPlayerIndex)
        {
            Debug.Log(GetLocalPlayerIndex() == losingPlayerIndex ? "You lost the game!" : "You won the game!");
            endTurnButton?.SetActive(false);
        }

        [ClientRpc]
        private void ClearAllSlotsClientRpc()
        {
            FieldManager.Instance.ClearAllSlots();
        }

        // ?? Helpers ????????????????????????????????????????????????????????????

        private void SyncDecksAndStats()
        {
            if (players.Count < 2) return;

            var p0 = players[0];
            var p1 = players[1];

            UpdateDeckClientRpc(deckManager.GetCardIds(p0.Deck), 0);
            UpdateDeckClientRpc(deckManager.GetCardIds(p1.Deck), 1);
            UpdatePlayerStatsClientRpc(
                p0.Hp, p0.CurrentMana, p0.MaxMana,
                p1.Hp, p1.CurrentMana, p1.MaxMana
            );
        }

        private int GetLocalPlayerIndex()
        {
            var localId = NetworkManager.Singleton.LocalClientId;
            return clientPlayerIndices.TryGetValue(localId, out int idx) ? idx : -1;
        }

        // ?? Phase Logic (Server Only) ??????????????????????????????????????????

        private void ManaGainPhase(int playerIndex)
        {
            if (playerIndex < 0 || playerIndex >= players.Count)
            {
                Debug.LogError($"ManaGainPhase: playerIndex {playerIndex} out of range.");
                return;
            }
            var player = players[playerIndex];
            if (player.MaxMana < 10) player.MaxMana++;
            player.CurrentMana = player.MaxMana;

            Debug.Log($"[Mana] Player {playerIndex + 1}: {player.CurrentMana}/{player.MaxMana}");
            SyncDecksAndStats();
        }

        private void DrawPhase(int playerIndex)
        {
            var player = players[playerIndex];
            if (player.Deck.Count == 0)
            {
                gameEnded.Value = true;
                EndGameClientRpc(playerIndex);
                return;
            }
            deckManager.DrawCard(player);
            UpdateHandClientRpc(deckManager.GetCardIds(player.Hand), playerIndex);
            UpdateDeckClientRpc(deckManager.GetCardIds(player.Deck), playerIndex);
            Debug.Log($"[Draw] Player {playerIndex + 1} drew. Hand: {player.Hand.Count}");
        }

        private void CastingPhase(int playerIndex)
        {
            Debug.Log($"[Casting] Player {playerIndex + 1} may cast.");
        }

        private void ResolutionPhase(int playerIndex)
        {
            Debug.Log($"[Resolution] Resolving stack for Player {playerIndex + 1}.");
        }

        private void EndPhase(int playerIndex)
        {
            Debug.Log($"[End] Player {playerIndex + 1} ends turn.");
        }

        /// <summary>Returns true if the local player is currently in the Casting phase.</summary>
        public bool IsLocalPlayerCasting()
        {
            return GetLocalPlayerIndex() == currentPlayerIndex.Value
                && CurrentPhase == TurnPhase.Casting
                && !gameEnded.Value;
        }

        /// <summary>Returns the local player's current mana, or 0 if not found.</summary>
        public int GetLocalPlayerCurrentMana()
        {
            int idx = GetLocalPlayerIndex();
            if (idx < 0 || idx >= players.Count) return 0;
            return players[idx].CurrentMana;
        }
    }
}