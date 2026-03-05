using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerStatsUI : MonoBehaviour
{
    [Header("Player Stats UI")]
    [SerializeField] private TextMeshProUGUI playerHpText;
    [SerializeField] private TextMeshProUGUI playerManaText;
    [SerializeField] private TextMeshProUGUI opponentHpText;
    [SerializeField] private TextMeshProUGUI opponentManaText;

    private int playerHp;
    private int playerMana;
    private int playerMaxMana;
    private int opponentHp;
    private int opponentMana;
    private int opponentMaxMana;

   

    public void UpdateStats(int playerHp, int playerMana, int playerMaxMana, int opponentHp, int opponentMana, int opponentMaxMana)
    {
        this.playerHp = playerHp;
        this.playerMana = playerMana;
        this.playerMaxMana = playerMaxMana;
        this.opponentHp = opponentHp;
        this.opponentMana = opponentMana;
        this.opponentMaxMana = opponentMaxMana;

        playerHpText.text = $"HP: {playerHp}";
        playerManaText.text = $"Mana: {playerMana}/{playerMaxMana}";
        opponentHpText.text = $"HP: {opponentHp}";
        opponentManaText.text = $"Mana: {opponentMana}/{opponentMaxMana}";
    }

    public void DamagePlayer(bool isPlayer, int amount)
    {
        if (isPlayer)
        {
            playerHp -= amount;
            if (playerHp < 0) playerHp = 0;
            playerHpText.text = $"HP: {playerHp}";
        }
        else
        {
            opponentHp -= amount;
            if (opponentHp < 0) opponentHp = 0;
            opponentHpText.text = $"HP: {opponentHp}";
        }
    }

    public void HealPlayer(bool isPlayer, int amount, int maxHp = 99)
    {
        if (isPlayer)
        {
            playerHp += amount;
            if (playerHp > maxHp) playerHp = maxHp;
            playerHpText.text = $"HP: {playerHp}";
        }
        else
        {
            opponentHp += amount;
            if (opponentHp > maxHp) opponentHp = maxHp;
            opponentHpText.text = $"HP: {opponentHp}";
        }
    }
}