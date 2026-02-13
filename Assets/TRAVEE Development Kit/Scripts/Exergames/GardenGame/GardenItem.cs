using UnityEngine;

namespace GardenGame
{
    public enum GardenItemType
    {
        SeedBag = 0,
        WateringCan = 1,
        Trowel = 2,
        Flower = 3
    }

    public class GardenItem : MonoBehaviour
    {
        public GardenItemType Type;
    }
}
