using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class TeenPattiEvaluator
{
    public static int GetHandScore(List<Card> hand)
    {
        if (hand == null || hand.Count < 3) return 0;

        // Convert ranks to numbers safely
        var ranks = hand.Select(c => GetRankValue(c.rank)).OrderBy(v => v).ToList();

        // Safety check to ensure we have 3 valid numbers
        if (ranks.Contains(0)) return 0;

        bool isColor = hand.All(c => c.suit == hand[0].suit);

        // Sequence check (including A,2,3)
        bool isSequence = (ranks[2] - ranks[1] == 1 && ranks[1] - ranks[0] == 1) ||
                          (ranks[0] == 2 && ranks[1] == 3 && ranks[2] == 14);

        // 1. Trail (Three of a kind)
        if (ranks[0] == ranks[2]) return 6000 + ranks[0];

        // 2. Pure Sequence
        if (isColor && isSequence) return 5000 + ranks[2];

        // 3. Sequence
        if (isSequence) return 4000 + ranks[2];

        // 4. Color (Flush)
        if (isColor) return 3000 + ranks[2];

        // 5. Pair
        if (ranks[0] == ranks[1] || ranks[1] == ranks[2])
            return 2000 + ranks[1];

        // 6. High Card
        return 1000 + ranks[2];
    }

    private static int GetRankValue(string r)
    {
        if (string.IsNullOrEmpty(r)) return 0;

        // Clean the string (Remove spaces and make it Uppercase)
        string rank = r.Trim().ToUpper();

        switch (rank)
        {
            case "A": return 14;
            case "K": return 13;
            case "Q": return 12;
            case "J": return 11;
            case "10": return 10;
            default:
                int val;
                if (int.TryParse(rank, out val)) return val;
                return 0; // If it's something we don't recognize
        }
    }
}