using CardGame.UI;
using System.Collections.Generic;
using UnityEngine;

namespace CardGame.Core
{
    public class HandUIManager : MonoBehaviour
    {
        [SerializeField] private GameObject cardPrefab;
        [SerializeField] private float fanSpread = 10f;
        [SerializeField] private float fanRadius = 8f;

        public void ShowHandUI(Player player, Transform handPanel, List<CardDataSO> handOverride = null, bool showBack = false)
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

            UpdateHandVisuals(cardsInHand, fanSpread, fanRadius);
        }

        public void ShowDeckUI(List<CardDataSO> deck, Transform deckPanel, bool showBack)
        {
            foreach (Transform child in deckPanel)
                Destroy(child.gameObject);

            float offset = 2.0f;
            float zOffset = -1.0f;

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

                cardGO.transform.localPosition = new Vector3(offset * i, -offset * i, zOffset * i);
                cardGO.transform.localRotation = Quaternion.identity;

                var canvas = cardGO.GetComponent<Canvas>();
                if (canvas != null)
                {
                    canvas.overrideSorting = true;
                    canvas.sortingOrder = i;
                }
            }
        }

        private void UpdateHandVisuals(List<GameObject> cardsInHand, float fanSpread, float fanRadius)
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
    }
}