using System.Collections.Generic;
using CardGame.Core;
using UnityEngine;
using BenchmarkDotNet.Attributes;
using Microsoft.VSDiagnostics.TestTools;
using Microsoft.VSDiagnostics;

namespace CardGame.Benchmarks
{
    [Microsoft.VSDiagnostics.TestTools.VSDiagnosticsExport]
    [CPUUsageDiagnoser]
    public class DeckManagerShuffleBenchmark
    {
        private List<CardDataSO> deck;
        private DeckManager deckManager;
        private Dictionary<string, CardDataSO> cardLookup;
        [GlobalSetup]
        public void Setup()
        {
            // Simulate 40 cards
            cardLookup = new Dictionary<string, CardDataSO>();
            for (int i = 0; i < 40; i++)
            {
                var card = ScriptableObject.CreateInstance<CardDataSO>();
                card.Name = $"Card{i}";
                cardLookup[card.Name] = card;
            }

            deck = new List<CardDataSO>(cardLookup.Values);
            deckManager = new DeckManager(cardLookup);
        }

        [Benchmark]
        public void ShuffleDeck()
        {
            deckManager.ShuffleDeck(deck);
        }
    }
}