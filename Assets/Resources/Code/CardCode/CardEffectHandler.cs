using UnityEngine;

namespace CardGame.Core
{
    /// <summary>
    /// Server-side resolution of card effects when a card is played onto the field.
    /// </summary>
    public static class CardEffectHandler
    {
        /// <summary>
        /// Applies the card's effect. Returns a <see cref="EffectResult"/> describing what happened.
        /// </summary>
        public static EffectResult Apply(CardDataSO card, Player owner, Player opponent)
        {
            var result = new EffectResult { Effect = card.Effect, Value = card.EffectValue };

            switch (card.Effect)
            {
                case CardEffect.Burn:
                    int burnDmg = card.EffectValue > 0 ? card.EffectValue : card.Attack;
                    opponent.Hp -= burnDmg;
                    if (opponent.Hp < 0) opponent.Hp = 0;
                    result.Value = burnDmg;
                    Debug.Log($"[Effect] Burn: opponent takes {burnDmg} damage. Opponent HP: {opponent.Hp}");
                    break;

                case CardEffect.Heal:
                    int healAmt = card.EffectValue;
                    owner.Hp += healAmt;
                    if (owner.Hp > 30) owner.Hp = 30;
                    result.Value = healAmt;
                    Debug.Log($"[Effect] Heal: owner restored {healAmt} HP. Owner HP: {owner.Hp}");
                    break;

                case CardEffect.Shield:
                    owner.ShieldCharges++;
                    result.Value = 1;
                    Debug.Log($"[Effect] Shield: owner gains a shield charge.");
                    break;

                case CardEffect.Draw:
                    result.Value = card.EffectValue > 0 ? card.EffectValue : 1;
                    Debug.Log($"[Effect] Draw: owner will draw {result.Value} card(s).");
                    break;

                case CardEffect.ManaBoost:
                    int boost = card.EffectValue;
                    owner.CurrentMana = Mathf.Min(owner.CurrentMana + boost, owner.MaxMana);
                    result.Value = boost;
                    Debug.Log($"[Effect] ManaBoost: owner gains {boost} mana. Mana: {owner.CurrentMana}/{owner.MaxMana}");
                    break;

                case CardEffect.None:
                default:
                    break;
            }

            return result;
        }
    }

    public struct EffectResult
    {
        public CardEffect Effect;
        public int Value;
    }
}