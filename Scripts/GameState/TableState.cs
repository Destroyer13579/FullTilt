using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Step 1: Basic data container for table state
/// This does NOT interact with the game yet - just defines the structure
/// </summary>
[Serializable]
public class TableState
{
    // === BASIC TABLE INFO ===
    public string tableId;
    public int handNumber;

    // === SEATS ===
    public List<SeatSnapshot> seats = new List<SeatSnapshot>();

    // === DEALER & BLINDS ===
    public int dealerButtonSeat;
    public int smallBlindSeat;
    public int bigBlindSeat;

    // === POT ===
    public int totalPot;

    // === CURRENT STREET ===
    public string currentStreet;  // "PreFlop", "Flop", "Turn", "River", "Showdown"

    // === BETTING STATE ===
    public bool bettingComplete;  // Has betting finished for current street?

    // === BOARD CARDS ===
    public List<string> boardCards = new List<string>();  // e.g., "Ah", "Kd", "Qs"

    // === WHOSE TURN ===
    public int currentPlayerSeat;  // -1 if no active turn
    public int currentBet;         // Amount needed to call

    public TableState()
    {
        // Initialize empty state
        tableId = "";
        handNumber = 0;
        dealerButtonSeat = -1;
        smallBlindSeat = -1;
        bigBlindSeat = -1;
        totalPot = 0;
        currentStreet = "PreFlop";
        currentPlayerSeat = -1;
        currentBet = 0;
    }
}

/// <summary>
/// Data for a single seat at the table
/// </summary>
[Serializable]
public class SeatSnapshot
{
    public int seatIndex;           // 0-8 (9 seats)
    public bool isOccupied;         // Is someone sitting here?
    public string playerName;       // Player's name
    public int chipCount;           // Current chips
    public bool hasFolded;          // Did they fold this hand?
    public bool isAllIn;            // Are they all-in?
    public bool isSittingOut;       // Are they sitting out?

    // Hole cards (empty if not visible)
    public List<string> holeCards = new List<string>();  // e.g., ["As", "Kh"]

    // Current bet this street
    public int currentBet;          // How much they've bet this street

    public SeatSnapshot()
    {
        seatIndex = -1;
        isOccupied = false;
        playerName = "";
        chipCount = 0;
        hasFolded = false;
        isAllIn = false;
        isSittingOut = false;
        currentBet = 0;
    }
}
