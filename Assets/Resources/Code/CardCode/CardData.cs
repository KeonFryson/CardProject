using UnityEngine;

public class Card
{
    public string Name { get; set; }
    public string Description { get; set; }
    public CardType Type { get; set; }
    public int ManaCost { get; set; }
    public int Attack { get; set; }
    public int Health { get; set; }
}

public enum CardType
{
    Spell,
    FocusCreature,
    Relic,
    Ritual
}

public enum CardEffect
{
    None,
    Burn,       // Deal damage to opponent
    Heal,       // Restore HP to self
    Shield,     // Negate next damage
    Draw,       // Draw a card
    ManaBoost   // Gain extra mana
}