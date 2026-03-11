//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;
//using UnityEngine;

//namespace Assets.Scripts
//{

//    [System.Serializable] // Makes it visible in the Inspector
//    public class Card
//    {
//        public string suit;  // Spades, Hearts, Clubs, Diamonds
//        public int rank;     // 1 (Ace) to 13 (King)
//        public int value;    
//        public Sprite cardImage;

//        // Constructor to create a card 
//        public Card(string _suit, int _rank, Sprite _sprite)
//        {
//            suit = _suit;
//            rank = _rank;
//            cardImage = _sprite;

//            // TEEN PATTI RULE: Ace is highest (14), others are normal
//            if (rank == 1) 
//                value = 14;
//            else 
//                value = rank;
//        }
//    }
//}
using UnityEngine;

[System.Serializable] // This makes the card show up in the Inspector
public class Card
{
    public string suit;       // Clubs, Spades, etc.
    public string rank;          // 1 (Ace) to 13 (King)
    public Sprite cardSprite; // The image of the card
   

    // This is a "Constructor" - it helps create a card in one line of code
    public Card(string _rank, string _suit, Sprite _sprite)
    {
        rank = _rank;
        suit = _suit;
        cardSprite = _sprite;
    }
}