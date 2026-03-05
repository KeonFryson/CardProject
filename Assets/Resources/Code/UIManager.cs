using System.Collections.Generic;
using UnityEngine;
using CardGame.Core;
using CardGame.UI;

namespace CardGame.Core
{
    public class UIManager
    {
        private Transform playersHandPanel;
        private Transform opponentsHandPanel;
        private Transform yourDeckPanel;
        private Transform opponentsDeckPanel;
        private GameObject cardPrefab;
        private float fanSpread;
        private float fanRadius;
        private PlayerStatsUI playerStatsUI;

        public UIManager(Transform playersHandPanel, Transform opponentsHandPanel, Transform yourDeckPanel, Transform opponentsDeckPanel, GameObject cardPrefab, float fanSpread, float fanRadius, PlayerStatsUI playerStatsUI)
        {
            this.playersHandPanel = playersHandPanel;
            this.opponentsHandPanel = opponentsHandPanel;
            this.yourDeckPanel = yourDeckPanel;
            this.opponentsDeckPanel = opponentsDeckPanel;
            this.cardPrefab = cardPrefab;
            this.fanSpread = fanSpread;
            this.fanRadius = fanRadius;
            this.playerStatsUI = playerStatsUI;
        }

        public void ShowHandUI(Player player, Transform handPanel, List<CardDataSO> handOverride = null, bool showBack = false)
        {
            foreach (Transform child in handPanel)
                Object.Destroy(child.gameObject);

            var handList = handOverride ?? player.Hand;
            int cardCount = handList.Count;
            if (cardCount == 0) return;

            var cardsInHand = new List<GameObject>();

            for (int i = 0; i < cardCount; i++)
            {
                var cardData = handList[i];
                var cardGO = Object.Instantiate(cardPrefab, handPanel);

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

            UpdateHandVisuals(cardsInHand);
        }

        public void ShowDeckUI(List<CardDataSO> deck, Transform deckPanel, bool showBack)
        {
            foreach (Transform child in deckPanel)
                Object.Destroy(child.gameObject);

            float offset = 2.0f; // Adjust for more/less overlap
            float zOffset = -1.0f; // To ensure correct layering in 3D space

            for (int i = 0; i < deck.Count; i++)
            {
                var cardData = deck[i];
                var cardGO = Object.Instantiate(cardPrefab, deckPanel);

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

        public void UpdateHandVisuals(List<GameObject> cardsInHand)
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

        public void UpdatePlayerStatsUI(Player player, Player opponent)
        {
            if (playerStatsUI == null)
                return;

            playerStatsUI.UpdateStats(
                player.Hp, player.CurrentMana, player.MaxMana,
                opponent.Hp, opponent.CurrentMana, opponent.MaxMana
            );
        }
    }
}
