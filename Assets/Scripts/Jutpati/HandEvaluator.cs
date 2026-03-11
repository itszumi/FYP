using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

    public static class HandEvaluator
    {
        // RULES: 
        // You hold 5 cards. You draw 1 (Total 6). 
        // To win, you need exactly 3 pairs.
        public static bool CheckWinCondition(List<Card> cards)
        {
            // 1. Basic validation: Must have even number of cards to make full pairs
            if (cards.Count % 2 != 0) return false;

            // 2. Group cards by Rank (e.g., all 7s, all Kings)
            // This counts how many of each card you have
            var groups = cards.GroupBy(c => c.rank);

            int pairsFound = 0;

            foreach (var group in groups)
            {
                int count = group.Count();

                // A "Jut" (Pair) is exactly 2 cards.
                // A "Quad" (4 cards) counts as 2 pairs.
                if (count == 2)
                {
                    pairsFound++;
                }
                else if (count == 4)
                {
                    pairsFound += 2;
                }
                else
                {
                    // If you have 1 card (Single) or 3 cards (Triplet), you CANNOT win yet.
                    return false;
                }
            }

            // 3. If total pairs * 2 equals total cards, you win.
            return (pairsFound * 2) == cards.Count;
        }
    }

