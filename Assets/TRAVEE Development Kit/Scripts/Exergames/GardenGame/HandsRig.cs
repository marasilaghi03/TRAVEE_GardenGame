using UnityEngine;

namespace GardenGame
{
    public class HandsRig : MonoBehaviour
    {
        public static HandsRig Instance { get; private set; }

        public Transform LeftPalm;
        public Transform RightPalm;

        void Awake()
        {
            Instance = this;
        }

        public Transform GetPalm(FruitsGame.BodySide side)
        {
            return side == FruitsGame.BodySide.BODY_SIDE_LEFT ? LeftPalm : RightPalm;
        }
    }
}
