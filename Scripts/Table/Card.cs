using System.Collections.Generic;
using UnityEngine;

public enum Suit
{
    Clubs,
    Diamonds,
    Hearts,
    Spades
}

public enum Rank
{
    Two = 2,
    Three = 3,
    Four = 4,
    Five = 5,
    Six = 6,
    Seven = 7,
    Eight = 8,
    Nine = 9,
    Ten = 10,
    Jack = 11,
    Queen = 12,
    King = 13,
    Ace = 14
}

[System.Serializable]
public class Card
{
    public Suit suit;
    public Rank rank;

    public Card(Suit suit, Rank rank)
    {
        this.suit = suit;
        this.rank = rank;
    }

    public string GetShortName()
    {
        string rankStr;
        switch (rank)
        {
            case Rank.Ten: rankStr = "T"; break;
            case Rank.Jack: rankStr = "J"; break;
            case Rank.Queen: rankStr = "Q"; break;
            case Rank.King: rankStr = "K"; break;
            case Rank.Ace: rankStr = "A"; break;
            default: rankStr = ((int)rank).ToString(); break;
        }

        string suitStr;
        switch (suit)
        {
            case Suit.Clubs: suitStr = "c"; break;
            case Suit.Diamonds: suitStr = "d"; break;
            case Suit.Hearts: suitStr = "h"; break;
            case Suit.Spades: suitStr = "s"; break;
            default: suitStr = "?"; break;
        }

        return rankStr + suitStr;
    }

    public override string ToString()
    {
        return $"{rank} of {suit}";
    }
}

public class Deck
{
    private List<Card> cards = new List<Card>();
    private int currentIndex = 0;

    public Deck()
    {
        Reset();
    }

    public void Reset()
    {
        cards.Clear();
        currentIndex = 0;

        foreach (Suit suit in System.Enum.GetValues(typeof(Suit)))
        {
            foreach (Rank rank in System.Enum.GetValues(typeof(Rank)))
            {
                cards.Add(new Card(suit, rank));
            }
        }
    }

    public void Shuffle()
    {
        currentIndex = 0;
        
        // Fisher-Yates shuffle
        for (int i = cards.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            Card temp = cards[i];
            cards[i] = cards[j];
            cards[j] = temp;
        }
    }

    public Card Deal()
    {
        if (currentIndex >= cards.Count)
        {
            Debug.LogWarning("Deck is empty!");
            return null;
        }

        return cards[currentIndex++];
    }

    public List<Card> Deal(int count)
    {
        List<Card> dealt = new List<Card>();
        for (int i = 0; i < count; i++)
        {
            Card card = Deal();
            if (card != null)
                dealt.Add(card);
        }
        return dealt;
    }

    public int CardsRemaining => cards.Count - currentIndex;
}
