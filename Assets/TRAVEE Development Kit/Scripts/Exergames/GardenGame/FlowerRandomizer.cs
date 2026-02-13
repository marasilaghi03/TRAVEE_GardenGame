using System.Collections.Generic;
using UnityEngine;

namespace GardenGame
{
    //flori random, mai diverse
    public static class FlowerRandomizer
    {
        private static readonly List<FlowerType> _deck = new List<FlowerType>(5);
        private static int _index = 0;

        private static readonly FlowerType[] _all =
        {
            FlowerType.RedRose,
            FlowerType.BlackRose,
            FlowerType.Dandelion,
            FlowerType.Hyacinth,
            FlowerType.Daisy
        };

        public static void ResetDeck(int? seed = null)
        {
            _deck.Clear();
            _deck.AddRange(_all);
            Shuffle(_deck, seed);
            _index = 0;
        }

        public static FlowerType Next()
        {
            if (_deck.Count == 0 || _index >= _deck.Count)
            {
                ResetDeck();
            }

            return _deck[_index++];
        }

        private static void Shuffle(List<FlowerType> list, int? seed)
        {
            if (seed.HasValue)
                Random.InitState(seed.Value);

            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
