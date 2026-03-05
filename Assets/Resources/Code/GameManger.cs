using System.Collections.Generic;
using Unity.Netcode;
using Unity.Collections;
using UnityEngine;

public class GameManger : NetworkBehaviour
{
    [SerializeField] private Transform playersHandPanel;
    [SerializeField] private Transform OppentsHandPanel;
    [SerializeField] private GameObject cardPrefab;
    [SerializeField] private float fanSpread = 10f;
    [SerializeField] private float fanRadius = 8f;

    private List<Player> players = new List<Player>();
    private Dictionary<ulong, int> clientPlayerIndices = new Dictionary<ulong, int>();
    private NetworkVariable<int> currentPlayerIndex = new NetworkVariable<int>();
    private NetworkVariable<bool> gameEnded = new NetworkVariable<bool>();

    // Cache all cards for lookup by name
    private Dictionary<string, CardDataSO> cardLookup;

    private void Awake()
    {
        Debug.Log("GameManager Awake: Loading card data...");
        cardLookup = new Dictionary<string, CardDataSO>();
        var allCards = Resources.LoadAll<CardDataSO>("Cards");
        foreach (var card in allCards)
        {
            cardLookup[card.Name] = card;
        }
    }

    public override void OnNetworkSpawn()
    {
        Debug.Log("GameManager OnNetworkSpawn: IsServer=" + IsServer);
        PositionHandPanels();
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

        if(IsHost)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
        }
    }
    private void PositionHandPanels()
    {
        // Example positions for anchored UI (adjust as needed)
        float playerHandY = 100f;      // Bottom of screen
        float opponentHandY = -50f;    // Top of screen

        // Set positions for both panels using RectTransform
        if (playersHandPanel != null && playersHandPanel is RectTransform playerRect)
        {
            playerRect.anchoredPosition = new Vector2(0, playerHandY);
            playerRect.anchorMin = new Vector2(0.5f, 0f); // Center bottom
            playerRect.anchorMax = new Vector2(0.5f, 0f);
            playerRect.pivot = new Vector2(0.5f, 0f);
        }
        if (OppentsHandPanel != null && OppentsHandPanel is RectTransform oppRect)
        {
            oppRect.anchoredPosition = new Vector2(0, opponentHandY);
            oppRect.anchorMin = new Vector2(0.5f, 1f); // Center top
            oppRect.anchorMax = new Vector2(0.5f, 1f);
            oppRect.pivot = new Vector2(0.5f, 1f);
        }
    }
    private void OnClientConnected(ulong clientId)
    {
        Debug.Log($"Client connected: {clientId}");
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

        var deck = CreateDeck();
        var player = new Player(deck);
        if (players.Count <= assignedIndex)
            players.Add(player);
        else
            players[assignedIndex] = player;

        ShuffleDeck(player.Deck);
        DrawStartingHand(player, 5);

        UpdateHandClientRpc(GetCardIds(player.Hand), assignedIndex);

        // If only one player, add a bot as player 2
        if (IsServer && clientPlayerIndices.Count == 1 && players.Count == 1)
        {
            Debug.Log("Spawning bot as player 2.");
            var botDeck = CreateDeck();
            var botPlayer = new Player(botDeck);
            players.Add(botPlayer);
            ShuffleDeck(botPlayer.Deck);
            DrawStartingHand(botPlayer, 5);

            // Optionally, update UI for bot hand (as opponent)
            UpdateHandClientRpc(GetCardIds(botPlayer.Hand), 1);
        }

        if (clientPlayerIndices.Count == 2)
        {
            currentPlayerIndex.Value = Random.Range(0, 2);
            gameEnded.Value = false;
            Debug.Log($"Player {currentPlayerIndex.Value + 1} goes first.");
            for (int i = 0; i < players.Count; i++)
                UpdateHandClientRpc(GetCardIds(players[i].Hand), i);
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
        var card = player.Deck[0];
        player.Hand.Add(card);
        player.Deck.RemoveAt(0);
        UpdateHandClientRpc(GetCardIds(player.Hand), playerIndex);
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
        UpdateHandClientRpc(GetCardIds(player.Hand), playerIndex);
        UpdateFieldClientRpc(GetCardIds(player.Field), playerIndex);
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
            ShowHandUI(new Player(new List<CardDataSO>()), playersHandPanel, handCards, false);
        }
        else
        {
            // Show opponent's hand (card backs only)
            ShowHandUI(new Player(new List<CardDataSO>()), OppentsHandPanel, handCards, true);
        }
    }


    [ClientRpc]
    private void UpdateFieldClientRpc(FixedString64Bytes[] fieldCardIds, int playerIndex)
    {
        // Implement field UI update here if needed, using cardLookup as above
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

    private int GetLocalPlayerIndex()
    {
        var localId = NetworkManager.Singleton.LocalClientId;
        return clientPlayerIndices.ContainsKey(localId) ? clientPlayerIndices[localId] : -1;
    }

    private void ShowHandUI(Player player, Transform handPanel, List<CardDataSO> handOverride = null, bool showBack = false)
    {
        foreach (Transform child in handPanel)
            Destroy(child.gameObject);

        var handList = handOverride ?? player.Hand;
        int cardCount = handList.Count;
        if (cardCount == 0) return;

        var cardsInHand = new List<GameObject>();

        for (int i = 0; i < cardCount; i++)
        {
            var cardData = handList[i];
            var cardGO = Instantiate(cardPrefab, handPanel);

            var cardUIScript = cardGO.GetComponent<CardUI>();
            if (cardUIScript != null)
            {
                cardUIScript.SetCard(cardData);
                cardUIScript.SetCardBackVisible(showBack); // <-- Add this line
            }
          
            cardsInHand.Add(cardGO);

            var canvas = cardGO.GetComponent<Canvas>();
            if (canvas != null)
            {
                canvas.overrideSorting = true;
                canvas.sortingOrder = i;
            }
        }

        UpdateHandVisuals(cardsInHand, fanSpread);
    }

    private void UpdateHandVisuals(List<GameObject> cardsInHand, float fanSpread)
    {
        int cardCount = cardsInHand.Count;
        if (cardCount == 0) return;

        float totalAngle = fanSpread * (cardCount - 1);
        float startAngle = -totalAngle / 2f;

        for (int i = 0; i < cardCount; i++)
        {
            float angle = startAngle + fanSpread * i;
            float rad = angle * Mathf.Deg2Rad;

            float x = Mathf.Sin(rad) * fanRadius;
            float y = -Mathf.Cos(rad) * fanRadius + fanRadius;

            cardsInHand[i].transform.localPosition = new Vector3(x, y, 0f);
            cardsInHand[i].transform.localRotation = Quaternion.Euler(0f, 0f, angle);
        }
    }

    private List<CardDataSO> CreateDeck()
    {
        var allCards = Resources.LoadAll<CardDataSO>("Cards");
        var deck = new List<CardDataSO>();

        int deckSize = Random.Range(30, 41);
        int cardIndex = 0;
        for (int i = 0; i < deckSize; i++)
        {
            deck.Add(allCards[cardIndex]);
            cardIndex++;
            if (cardIndex >= allCards.Length)
            {
                cardIndex = 0;
            }
        }
        return deck;
    }

    private void ShuffleDeck(List<CardDataSO> deck)
    {
        for (int i = deck.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            var temp = deck[i];
            deck[i] = deck[j];
            deck[j] = temp;
        }
    }

    private void DrawStartingHand(Player player, int count)
    {
        for (int i = 0; i < count; i++)
        {
            DrawCard(player);
        }
    }

    private void DrawCard(Player player)
    {
        if (player.Deck.Count == 0)
        {
            gameEnded.Value = true;
            return;
        }
        var card = player.Deck[0];
        player.Hand.Add(card);
        player.Deck.RemoveAt(0);
    }

    // Helper: get array of card IDs from a list
    private FixedString64Bytes[] GetCardIds(List<CardDataSO> cards)
    {
        var ids = new FixedString64Bytes[cards.Count];
        for (int i = 0; i < cards.Count; i++)
        {
            ids[i] = new FixedString64Bytes(cards[i].Name);
        }
        return ids;
    }
}