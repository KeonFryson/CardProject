using System.Collections.Generic;

namespace CardGame.Core
{
    public class Player
    {
        public int Hp { get; set; } = 20;
        public int MaxMana { get; set; } = 1;
        public int CurrentMana { get; set; } = 1;
        public int ShieldCharges { get; set; } = 0;
        public List<CardDataSO> Deck { get; set; }
        public List<CardDataSO> Hand { get; set; }
        public List<CardDataSO> Field { get; set; }

        public Player(List<CardDataSO> deck)
        {
            Deck = deck;
            Hand = new List<CardDataSO>();
            Field = new List<CardDataSO>();
            Hp = 30;
            CurrentMana = 1;
            MaxMana = 1;
            ShieldCharges = 0;
        }
    }
}