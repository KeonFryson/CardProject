using System.Collections.Generic;
using UnityEngine;
using CardGame.Core;
using Unity.Collections;

namespace CardGame.Core
{
    public class DeckManager
    {
        private Dictionary<string, CardDataSO> cardLookup;

        public DeckManager(Dictionary<string, CardDataSO> cardLookup)
        {
            this.cardLookup = cardLookup;
        }

        public List<CardDataSO> CreateDeck()
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

        public void ShuffleDeck(List<CardDataSO> deck)
        {
            for (int i = deck.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                var temp = deck[i];
                deck[i] = deck[j];
                deck[j] = temp;
            }
        }

        public void DrawStartingHand(Player player, int count)
        {
            for (int i = 0; i < count; i++)
            {
                DrawCard(player);
            }
        }

        public void DrawCard(Player player)
        {
            if (player.Deck.Count == 0)
                return;
            var card = player.Deck[0];
            player.Hand.Add(card);
            player.Deck.RemoveAt(0);
        }

        // Helper: get array of card IDs from a list
        public FixedString64Bytes[] GetCardIds(List<CardDataSO> cards)
        {
            var ids = new FixedString64Bytes[cards.Count];
            for (int i = 0; i < cards.Count; i++)
            {
                ids[i] = new FixedString64Bytes(cards[i].Name);
            }
            return ids;
        }
    }
}
