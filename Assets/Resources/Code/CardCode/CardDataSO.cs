using UnityEngine;

[CreateAssetMenu(fileName = "NewCardData", menuName = "Card/Create New Card")]
public class CardDataSO : ScriptableObject
{
    public string Name => name; // Uses ScriptableObject.name

    [SerializeField]
    private string description;
    public string Description => description;

    [SerializeField]
    private CardType type;
    public CardType Type => type;

    [SerializeField]
    private int manaCost;
    public int ManaCost => manaCost;

    [SerializeField]
    private int attack;
    public int Attack => attack;

    

}