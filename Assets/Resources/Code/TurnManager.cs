using System;
using UnityEngine;

namespace CardGame.Core
{
    public class TurnManager : MonoBehaviour
    {
        public static TurnManager Instance { get; private set; }

        public event Action<int> OnTurnStart;
        public event Action<int> OnManaGainPhase;
        public event Action<int> OnDrawPhase;
        public event Action<int> OnCastingPhase;
        public event Action<int> OnResolutionPhase;
        public event Action<int> OnEndPhase;

        private int currentPlayerIndex = 0;
        private int playerCount = 2;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        public void Initialize(int playerCount)
        {
            this.playerCount = playerCount;
            currentPlayerIndex = 0;
        }

        public void StartTurn()
        {
            OnTurnStart?.Invoke(currentPlayerIndex);
        }

        // Manual phase triggers
        public void TriggerManaGainPhase()
        {
            OnManaGainPhase?.Invoke(currentPlayerIndex);
        }

        public void TriggerDrawPhase()
        {
            OnDrawPhase?.Invoke(currentPlayerIndex);
        }

        public void TriggerCastingPhase()
        {
            OnCastingPhase?.Invoke(currentPlayerIndex);
        }

        public void TriggerResolutionPhase()
        {
            OnResolutionPhase?.Invoke(currentPlayerIndex);
        }

        public void TriggerEndPhase()
        {
            OnEndPhase?.Invoke(currentPlayerIndex);
            PassTurn();
        }

        private void PassTurn()
        {
            currentPlayerIndex = (currentPlayerIndex + 1) % playerCount;
            StartTurn();
        }

        public int GetCurrentPlayerIndex()
        {
            return currentPlayerIndex;
        }

        public void EndTurn()
        {
            TriggerEndPhase();
        }
    }
}