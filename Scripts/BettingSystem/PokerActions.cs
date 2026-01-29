using UnityEngine;

/// <summary>
/// Player action types in poker
/// </summary>
public enum PokerAction
{
    Fold,
    Check,
    Call,
    Bet,
    Raise,
    AllIn
}

/// <summary>
/// Data about a player's action
/// </summary>
public class PlayerActionData
{
    public PokerAction action;
    public int amount;      // Bet/raise amount (0 for fold/check/call)
    public string playerName;

    public PlayerActionData(PokerAction action, int amount = 0, string playerName = "")
    {
        this.action = action;
        this.amount = amount;
        this.playerName = playerName;
    }

    public override string ToString()
    {
        switch (action)
        {
            case PokerAction.Fold:
                return $"{playerName} folds";
            case PokerAction.Check:
                return $"{playerName} checks";
            case PokerAction.Call:
                return $"{playerName} calls ${amount}";
            case PokerAction.Bet:
                return $"{playerName} bets ${amount}";
            case PokerAction.Raise:
                return $"{playerName} raises to ${amount}";
            case PokerAction.AllIn:
                return $"{playerName} goes all-in for ${amount}";
            default:
                return $"{playerName} acts";
        }
    }
}

/// <summary>
/// Current betting state information
/// </summary>
public class BettingState
{
    public int amountToCall;        // Amount player needs to call (0 if can check)
    public int currentBet;          // Total current bet (for calculating raises)
    public int minimumRaise;        // Minimum raise amount above current bet
    public int pot;                 // Total pot
    public bool canCheck;           // Can player check?
    public bool canBet;             // Can player bet?
    public bool canRaise;           // Can player raise?

    public BettingState(int amountToCall, int currentBet, int minimumRaise, int pot)
    {
        this.amountToCall = amountToCall;
        this.currentBet = currentBet;
        this.minimumRaise = minimumRaise;
        this.pot = pot;

        // Determine available actions
        canCheck = (amountToCall == 0);
        canBet = (currentBet == 0);
        canRaise = (currentBet > 0);
    }
}
