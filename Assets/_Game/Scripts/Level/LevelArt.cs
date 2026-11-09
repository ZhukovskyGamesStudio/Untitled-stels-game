using UnityEngine;

namespace Stealth.Level
{
    /// <summary>
    /// Every prefab the level builder instantiates. Created by the asset builder (Stealth / Build Prototype Assets),
    /// then freely editable: swap a prefab here to restyle the whole office.
    /// </summary>
    [CreateAssetMenu(menuName = "Stealth/Level Art")]
    public class LevelArt : ScriptableObject
    {
        [Header("Root (NavMeshSurface)")]
        public GameObject LevelRoot;

        [Header("Structure: unit cubes scaled by the builder")]
        public GameObject WallBlock;
        public GameObject TrimBlock;
        public GameObject FloorSlab;
        public GameObject Roof;
        public GameObject OutsideGround;
        public GameObject WindowPane;
        public GameObject Fence;

        [Header("Pieces")]
        public GameObject Stairs;
        public GameObject StairsMirrored;
        public GameObject VentFrame;
        public GameObject AutoDoor;
        public GameObject RoomLight;
        public GameObject StreetLamp;

        [Header("Props")]
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

        public GameObject Prop(PropKind kind) => kind switch
        {
            PropKind.Desk => Desk,
            PropKind.Chair => Chair,
            PropKind.ServerRack => ServerRack,
            PropKind.Cabinet => Cabinet,
            PropKind.MeetingTable => MeetingTable,
            PropKind.Plant => Plant,
            PropKind.KitchenCounter => KitchenCounter,
            PropKind.Hatch => Hatch,
            PropKind.Shelf => Shelf,
            PropKind.Boxes => Boxes,
            PropKind.WaterCooler => WaterCooler,
            PropKind.Whiteboard => Whiteboard,
            PropKind.Sofa => Sofa,
            PropKind.ToiletStall => ToiletStall,
            _ => null,
        };
    }
}
