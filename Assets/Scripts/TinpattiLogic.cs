using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using UnityEngine;

public class TinpattiLogic : MonoBehaviour
{
    public static string[] suits = new string[] { "Clubs", "Diamonds", "Hearts", "Spades" };

    public static string[] values = new string[] { "A", "2", "3", "4", "5", "6", "7", "8", "9", "10", "J", "Q", "K" };

    public List<string> deck;
    void Start()
    {
        PlayGame();
        
    }


    public void PlayGame() { 
        deck = GenerateDeck();
        Shuffle(deck);
        foreach (string card in deck) {
            print(card);
        }
    }

    public static List<string> GenerateDeck() { 
    
        List<string> newDeck = new List<string>();
        foreach (string s in suits) { 
            
            foreach (string v in values) { 
                newDeck.Add(v + s);
            }

        }
        return newDeck;
    }

    void Shuffle<T>(List<T> list) {
        System.Random random = new System.Random();
        int n = list.Count;
        while (n > 1) {

            int k = random.Next(n);
            n--;
            T temp = list[k];
            list[k] = list[n];
            list[n] = temp;

        }
    
    
    }
}
