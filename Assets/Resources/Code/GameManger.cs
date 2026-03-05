using System.Collections.Generic;
using Unity.Netcode;
using Unity.Collections;
using UnityEngine;

public class GameManger : NetworkBehaviour
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

    private void UpdatePlayerStatsUI()
    {
        if (players.Count < 2 || playerStatsUI == null)
            return;

        // Assume player 0 is local, player 1 is opponent
        var localIndex = GetLocalPlayerIndex();
        var opponentIndex = localIndex == 0 ? 1 : 0;

        var player = players[localIndex];
        var opponent = players[opponentIndex];

        playerStatsUI.UpdateStats(
            player.Hp, player.CurrentMana, player.MaxMana,
            opponent.Hp, opponent.CurrentMana, opponent.MaxMana
        );
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



        if (players.Count == 2)
        {
            UpdateDeckClientRpc(GetCardIds(players[0].Deck), 0);
            UpdateDeckClientRpc(GetCardIds(players[1].Deck), 1);

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
        var card = player.Deck[0];
        player.Hand.Add(card);
        player.Deck.RemoveAt(0);
        UpdateHandClientRpc(GetCardIds(player.Hand), playerIndex);

        // Update deck visuals after draw
        if (playerIndex == 0)
            UpdateDeckClientRpc(GetCardIds(player.Deck), playerIndex);
        else
            UpdateDeckClientRpc(GetCardIds(player.Deck), playerIndex);

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
        UpdateHandClientRpc(GetCardIds(player.Hand), playerIndex);
        UpdateFieldClientRpc(GetCardIds(player.Field), playerIndex);

        // Update deck visuals after play
        if (playerIndex == 0)
            UpdateDeckClientRpc(GetCardIds(player.Deck), playerIndex);
        else
            UpdateDeckClientRpc(GetCardIds(player.Deck), playerIndex);

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
            ShowHandUI(new Player(new List<CardDataSO>()), playersHandPanel, handCards, false);
        }
        else
        {
            // Show opponent's hand (card backs only)
            ShowHandUI(new Player(new List<CardDataSO>()), opponentsHandPanel, handCards, true);
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
                cardUIScript.SetCardBackVisible(showBack);
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
            ShowDeckUI(deckCards, yourDeckPanel, true);
        }
        else
        {
            ShowDeckUI(deckCards, opponentsDeckPanel, true);
        }
    }


    private void ShowDeckUI(List<CardDataSO> deck, Transform deckPanel, bool showBack)
    {
        foreach (Transform child in deckPanel)
            Destroy(child.gameObject);

        float offset = 2.0f; // Adjust for more/less overlap
        float zOffset = -1.0f; // To ensure correct layering in 3D space

        for (int i = 0; i < deck.Count; i++)
        {
            var cardData = deck[i];
            var cardGO = Instantiate(cardPrefab, deckPanel);

            var cardUIScript = cardGO.GetComponent<CardUI>();
            if (cardUIScript != null)
            {
                cardUIScript.SetCard(cardData);
                cardUIScript.SetCardBackVisible(showBack);
            }

            // Stacked look: each card is slightly offset
            cardGO.transform.localPosition = new Vector3(offset * i, -offset * i, zOffset * i);
            cardGO.transform.localRotation = Quaternion.identity;
             

            // Ensure correct sorting order if using Canvas
            var canvas = cardGO.GetComponent<Canvas>();
            if (canvas != null)
            {
                canvas.overrideSorting = true;
                canvas.sortingOrder = i;
            }
        }
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