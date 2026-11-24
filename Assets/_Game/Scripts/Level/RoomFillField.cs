using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Stealth.Level
{
    /// <summary>
    /// Hands the picture the house's room fill (<see cref="RoomFill"/>): sums the ceiling lamps burning in every room,
    /// bakes the cells when that changes (a switch, an alarm) and keeps them in a buffer the full-screen pass reads
    /// (Art/Shaders/ScreenBounce.shader). Every peer, rendering only; ticked by <see cref="LightBudget"/> on the level's root.
    /// </summary>
    public sealed class RoomFillField : IDisposable
    {
        static readonly int CellsId = Shader.PropertyToID("_RoomFillCells"), GridId = Shader.PropertyToID("_RoomFillGrid"),
            HeightsId = Shader.PropertyToID("_RoomFillHeights"), OriginId = Shader.PropertyToID("_RoomFillOrigin"), StrengthId = Shader.PropertyToID("_RoomFillStrength");

        /// <summary>How strong the style shows the fill (StyleGrade.RoomFill; 0 = none).</summary>
        public static float Strength
        {
            get => s_Strength;
            set
            {
                s_Strength = Mathf.Max(0f, value);
                Shader.SetGlobalFloat(StrengthId, s_Strength);
            }
        }
        static float s_Strength;

        LevelLayout m_Layout;
        RoomFill.Grid m_Grid;
        GraphicsBuffer m_Buffer;
        uint[] m_Cells;
        Color[] m_Rooms, m_Baked;
        float m_Next;

        /// <summary>The rooms' lamps as they burn now, and a new bake if they changed.</summary>
        public void Tick(float now)
        {
            var layout = ActiveLevel.Layout;
            if (layout != m_Layout) Adopt(layout);
            if (m_Grid == null || now < m_Next) return;
            Sum(m_Layout, m_Rooms);
            bool changed = false;
            for (int i = 0; i < m_Rooms.Length && !changed; i++)
                changed = Mathf.Abs(m_Rooms[i].r - m_Baked[i].r) + Mathf.Abs(m_Rooms[i].g - m_Baked[i].g) + Mathf.Abs(m_Rooms[i].b - m_Baked[i].b) > 0.004f;
            if (!changed) return;
            m_Next = now + 0.1f;
            Array.Copy(m_Rooms, m_Baked, m_Rooms.Length);
            m_Grid.Bake(m_Rooms, m_Cells);
            m_Buffer.SetData(m_Cells);
        }

        /// <summary>The level of every room of a house from the lamps that stand in the world (<see cref="RoomLight.All"/>).</summary>
        public static void Sum(LevelLayout layout, Color[] rooms)
        {
            for (int i = 0; i < rooms.Length; i++) rooms[i] = Color.clear;
            var plan = layout.Plan;
            foreach (var lamp in RoomLight.All)
            {
                if (lamp == null) continue;
                var at = lamp.Lamp != null ? lamp.Lamp.transform.position : lamp.transform.position;
                int room = plan.SpaceAt(Mathf.Clamp(LevelLayout.FloorOf(at.y - 0.5f), 0, plan.Floors - 1), new Vector2(at.x, at.z));
                if (room >= 0 && room < rooms.Length) rooms[room] += lamp.Output;
            }
            for (int i = 0; i < rooms.Length; i++) rooms[i] = RoomFill.Level(rooms[i], plan.Rooms[i].Area, 1f);
        }

        /// <summary>The holes in the slabs the stairs go up through (the fill of the room below reaches up them).</summary>
        public static IEnumerable<(int floor, Rect hole)> Holes(LevelLayout layout)
        {
            foreach (var s in layout.Stairs) yield return (s.Floor, PlanGenerator.HoleOf(s));
        }

        void Adopt(LevelLayout layout)
        {
            Release();
            m_Layout = layout;
            if (layout?.Plan == null || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) return;
            Open(layout.Plan, Holes(layout), Vector2.zero);
            m_Next = 0f;
        }

        /// <summary>A plan's fill at given levels of its rooms, set where the plan's corner is in the world (the picture tools: a room without a house).</summary>
        public static RoomFillField Show(HousePlan plan, Color[] levels, Vector2 origin)
        {
            var field = new RoomFillField();
            field.Open(plan, null, origin);
            field.m_Grid.Bake(levels, field.m_Cells);
            field.m_Buffer.SetData(field.m_Cells);
            return field;
        }

        void Open(HousePlan plan, IEnumerable<(int floor, Rect hole)> holes, Vector2 origin)
        {
            m_Grid = new RoomFill.Grid(plan, holes);
            m_Cells = new uint[m_Grid.Count];
            m_Rooms = new Color[plan.Rooms.Count];
            m_Baked = new Color[m_Rooms.Length];
            for (int i = 0; i < m_Baked.Length; i++) m_Baked[i] = new Color(-1f, -1f, -1f); // (the first bake always happens)
            Shader.SetGlobalVector(OriginId, new Vector4(origin.x, origin.y, 0f, 0f));
            m_Buffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, m_Grid.Count, sizeof(uint)) { name = "RoomFill" };
            m_Buffer.SetData(m_Cells);
            Shader.SetGlobalBuffer(CellsId, m_Buffer);
            Shader.SetGlobalVector(GridId, new Vector4(m_Grid.NX, m_Grid.NZ, m_Grid.Floors, 1f / HousePlan.Res));
            Shader.SetGlobalVector(HeightsId, new Vector4(LevelMetrics.FloorHeight, LevelMetrics.WallHeight + 0.05f, RoomFill.NormalOffset, RoomFill.MaxLevel));
        }

        void Release()
        {
            if (m_Buffer != null)
            {
                Shader.SetGlobalVector(GridId, Vector4.zero); // (no floors: the pass reads nothing)
                m_Buffer.Dispose();
            }
            m_Buffer = null;
            m_Grid = null;
        }

        public void Dispose()
        {
            Release();
            m_Layout = null;
        }
    }
}
