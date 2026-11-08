using UnityEngine;

namespace Stealth.Level
{
    /// <summary>Visual building blocks for the generated office. Filled by the prototype builder.</summary>
    [CreateAssetMenu(menuName = "Stealth/Level Art")]
    public class LevelArt : ScriptableObject
    {
        [Header("Materials")]
        public Material Wall;
        public Material Floor;
        public Material Ceiling;
        public Material OutsideGround;
        public Material DoorFrame;

        [Header("Props (non-networked)")]
        public GameObject Desk;
        public GameObject Chair;
        public GameObject ServerRack;
        public GameObject Cabinet;
        public GameObject MeetingTable;
        public GameObject Plant;
        public GameObject KitchenCounter;
        public GameObject Hatch;
        public GameObject Shelf;
        public GameObject Boxes;
        public GameObject WaterCooler;
        public GameObject Whiteboard;
        public GameObject Sofa;
        public GameObject ToiletStall;
    }
}
