// This file is a Modification (MPL-2.0, section 1.10) of the original
// Performance Fish by bradson (https://github.com/bbradson/Performance-Fish),
// which is covered by the Mozilla Public License 2.0.
//
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using RimWorld;
using UnityEngine;
using Verse;

namespace PerformanceFishReforjed.Caching
{
    /// <summary>
    /// Información estática de configuración para un tipo de gas particular.
    /// Contiene los valores de disipación, difusión y flag de verificación de DLC.
    /// </summary>
    public sealed class GasInfo
    {
        public readonly GasType GasType;
        public readonly int DissipationRate;
        public readonly bool Diffuses;
        public readonly Color Color;
        public readonly int GasIndex; // 0=Smoke, 1=ToxGas, 2=RotStink, 3=DeadlifeDust

        public GasInfo(GasType gasType, int dissipationRate, bool diffuses, Color color, int gasIndex)
        {
            GasType = gasType;
            DissipationRate = dissipationRate;
            Diffuses = diffuses;
            Color = color;
            GasIndex = gasIndex;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsActive()
        {
            switch (GasType)
            {
                case GasType.ToxGas:
                    return ModsConfig.BiotechActive;
                case GasType.DeadlifeDust:
                    return ModsConfig.AnomalyActive;
                default:
                    return true;
            }
        }
    }

    /// <summary>
    /// Rejilla individual para un único tipo de gas, optimizada con un bitmap de cobertura de 64 bits.
    /// Permite saltarse bloques completos de 64 celdas sin gas en O(1).
    /// </summary>
    public sealed class ParallelGasGrid
    {
        private readonly ulong[] _gasCoverageForIndicesInRandomOrder;
        private readonly byte[] _gasDensity;
        private readonly GasInfo _gasInfo;
        private readonly Map _map;
        private readonly int _cellCount;

        private List<IntVec3> _cellsInRandomOrder = null!;
        private int[] _cellIndicesInRandomOrder = null!;
        private int[] _sourceIndicesOfRandomCells = null!;
        private readonly IntVec3[] _cardinalDirections;

        public int CycleIndexDiffusion;
        public int CycleIndexDissipation;
        public bool AnyGasEverAdded;

        public int CellCount => _cellCount;
        public GasInfo GasInfo => _gasInfo;
        public Map Map => _map;
        public int DissipationRate => _gasInfo.DissipationRate;
        public bool Diffuses => _gasInfo.Diffuses;

        public ParallelGasGrid(Map map, GasInfo gasInfo)
        {
            _map = map;
            _gasInfo = gasInfo;
            _cellCount = map.cellIndices.NumGridCells;

            _gasDensity = new byte[_cellCount];
            _gasCoverageForIndicesInRandomOrder = new ulong[((_cellCount - 1) >> 6) + 1];
            _cellIndicesInRandomOrder = new int[_cellCount];
            _sourceIndicesOfRandomCells = new int[_cellCount];

            _cardinalDirections = new IntVec3[GenAdj.CardinalDirections.Length];
            Array.Copy(GenAdj.CardinalDirections, _cardinalDirections, GenAdj.CardinalDirections.Length);

            InitRandomCells();
        }

        private void InitRandomCells()
        {
            _cellsInRandomOrder = _map.cellsInRandomOrder.GetAll();
            int mapSizeX = _map.Size.x;
            for (int i = _cellIndicesInRandomOrder.Length; i-- > 0;)
            {
                _sourceIndicesOfRandomCells[
                    _cellIndicesInRandomOrder[i] = CellIndicesUtility.CellToIndex(_cellsInRandomOrder[i], mapSizeX)] = i;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int CellToIndex(IntVec3 cell) => CellIndicesUtility.CellToIndex(cell, _map.Size.x);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IntVec3 IndexToCell(int index) => CellIndicesUtility.IndexToCell(index, _map.Size.x);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool AnyGasAt(int idx) => _gasDensity[idx] > 0;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool AnyGasAt(IntVec3 cell) => AnyGasAt(CellToIndex(cell));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte DensityAt(int index) => _gasDensity[index];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte DensityAt(IntVec3 cell) => DensityAt(CellToIndex(cell));

        public void SetDirect(int index, byte density)
        {
            _gasDensity[index] = density;

            int sourceIdx = _sourceIndicesOfRandomCells[index];
            ref ulong gasCoverageBucket = ref _gasCoverageForIndicesInRandomOrder[sourceIdx >> 6];
            bool hasGas = density > 0;

            if (hasGas)
                gasCoverageBucket |= (1UL << (sourceIdx & 63));
            else
                gasCoverageBucket &= ~(1UL << (sourceIdx & 63));

            AnyGasEverAdded |= hasGas;
        }

        public void SetDirect(IntVec3 cell, byte density) => SetDirect(CellToIndex(cell), density);

        public void AddGas(IntVec3 cell, int amount, bool canOverflow = true)
        {
            if (amount <= 0 || !GasCanMoveTo(cell) || !_gasInfo.IsActive())
                return;

            AnyGasEverAdded = true;
            int index = CellToIndex(cell);

            int newDensity = _gasDensity[index] + amount;
            byte clampedDensity;
            int overflow;

            if (newDensity > 255)
            {
                overflow = newDensity - 255;
                clampedDensity = 255;
            }
            else
            {
                overflow = 0;
                clampedDensity = (byte)newDensity;
            }

            SetDirect(index, clampedDensity);
            _map.mapDrawer.MapMeshDirty(cell, MapMeshFlagDefOf.Gas);

            if (canOverflow && overflow > 0)
                Overflow(cell, overflow);
        }

        private void Overflow(IntVec3 cell, int amount)
        {
            if (amount <= 0)
                return;

            int remainingAmount = amount;

            _map.floodFiller.FloodFill(cell, GasCanMoveTo, c =>
            {
                int num = Mathf.Min(remainingAmount, 255 - DensityAt(c));
                if (num > 0)
                {
                    AddGas(c, num, false);
                    remainingAmount -= num;
                }

                return remainingAmount <= 0;
            }, GenRadial.NumCellsInRadius(40f), true);
        }

        public void Clear()
        {
            Array.Clear(_gasCoverageForIndicesInRandomOrder, 0, _gasCoverageForIndicesInRandomOrder.Length);
            Array.Clear(_gasDensity, 0, _gasDensity.Length);
            AnyGasEverAdded = false;
        }

        public void Tick()
        {
            if (!AnyGasEverAdded)
                return;

            int area = _map.Area;

            // Disipación: procesa ~1/64 del mapa por tick
            TickDissipation(area, (area + 63) >> 6);

            // Difusión: procesa ~1/32 del mapa por tick si el gas difunde
            if (Diffuses)
                TickDiffusion(area, (area + 31) >> 5);

            if (Gen.IsHashIntervalTick(_map, 600))
                RecalculateEverHadGas();
        }

        private void TickDissipation(int area, int cellCountToTick)
        {
            int cellCycleIndex = CycleIndexDissipation;

            if ((cellCycleIndex & 63) != 0)
                TickOddCellsDissipation(area, ref cellCycleIndex);

            int buckets = (cellCountToTick >> 6) + 1;
            for (int i = 0; i < buckets; i++)
            {
                if (area - cellCycleIndex < 64)
                    TickOddCellsDissipation(area, ref cellCycleIndex);

                ulong gasCoverage = _gasCoverageForIndicesInRandomOrder[cellCycleIndex >> 6];
                if (gasCoverage != 0)
                    TickBucketDissipation(gasCoverage, cellCycleIndex);

                cellCycleIndex += 64;
                if (cellCycleIndex >= area)
                    cellCycleIndex = 0;
            }

            CycleIndexDissipation = cellCycleIndex;
        }

        private void TickBucketDissipation(ulong gasCoverage, int cellCycleIndex)
        {
            for (int i = 0; i < 64;)
            {
                ulong slice = (gasCoverage >> i) & 0xFF;
                if (slice == 0)
                {
                    i += 8;
                    continue;
                }

                for (int j = 0; j < 8; j++)
                {
                    if (((slice >> j) & 1UL) != 0)
                    {
                        int randomIdx = cellCycleIndex + i;
                        if (randomIdx < _cellIndicesInRandomOrder.Length)
                            DissipateGasAt(_cellIndicesInRandomOrder[randomIdx]);
                    }
                    i++;
                }
            }
        }

        private void TickOddCellsDissipation(int area, ref int cellCycleIndex)
        {
            if (cellCycleIndex < area)
            {
                ulong gasCoverage = _gasCoverageForIndicesInRandomOrder[cellCycleIndex >> 6];

                do
                {
                    if ((cellCycleIndex & 63) == 0)
                        return;

                    if (((gasCoverage >> (cellCycleIndex & 63)) & 1UL) != 0)
                    {
                        if (cellCycleIndex < _cellIndicesInRandomOrder.Length)
                            DissipateGasAt(_cellIndicesInRandomOrder[cellCycleIndex]);
                    }

                    cellCycleIndex++;
                }
                while (cellCycleIndex < area);
            }

            cellCycleIndex = 0;
        }

        private void DissipateGasAt(int index)
        {
            int currentDensity = _gasDensity[index];
            if (currentDensity == 0)
                return;

            IntVec3 cell = IndexToCell(index);
            float vacuum = VacuumUtility.GetVacuum(cell, _map);
            float factor = GridsUtility.Roofed(cell, _map) ? 0.5f : 1f;
            factor += vacuum * 25f;

            int dissipation = Mathf.RoundToInt(DissipationRate * factor);
            int newDensity = Math.Max(currentDensity - dissipation, 0);

            SetDirect(index, (byte)newDensity);

            if (newDensity == 0)
                _map.mapDrawer.MapMeshDirty(cell, MapMeshFlagDefOf.Gas);
        }

        private void TickDiffusion(int area, int cellCountToTick)
        {
            int cellCycleIndex = CycleIndexDiffusion;

            if ((cellCycleIndex & 63) != 0)
                TickOddCellsDiffusion(area, ref cellCycleIndex);

            int buckets = (cellCountToTick >> 6) + 1;
            for (int i = 0; i < buckets; i++)
            {
                if (area - cellCycleIndex < 64)
                    TickOddCellsDiffusion(area, ref cellCycleIndex);

                ulong gasCoverage = _gasCoverageForIndicesInRandomOrder[cellCycleIndex >> 6];
                if (gasCoverage != 0)
                    TickBucketDiffusion(gasCoverage, cellCycleIndex);

                cellCycleIndex += 64;
                if (cellCycleIndex >= area)
                    cellCycleIndex = 0;
            }

            CycleIndexDiffusion = cellCycleIndex;
        }

        private void TickBucketDiffusion(ulong gasCoverage, int cellCycleIndex)
        {
            for (int i = 0; i < 64;)
            {
                ulong slice = (gasCoverage >> i) & 0xFF;
                if (slice == 0)
                {
                    i += 8;
                    continue;
                }

                for (int j = 0; j < 8; j++)
                {
                    if (((slice >> j) & 1UL) != 0)
                    {
                        int randomIdx = cellCycleIndex + i;
                        if (randomIdx < _cellsInRandomOrder.Count)
                            DiffuseGasAt(_cellsInRandomOrder[randomIdx]);
                    }
                    i++;
                }
            }
        }

        private void TickOddCellsDiffusion(int area, ref int cellCycleIndex)
        {
            if (cellCycleIndex < area)
            {
                ulong gasCoverage = _gasCoverageForIndicesInRandomOrder[cellCycleIndex >> 6];

                do
                {
                    if ((cellCycleIndex & 63) == 0)
                        return;

                    if (((gasCoverage >> (cellCycleIndex & 63)) & 1UL) != 0)
                    {
                        if (cellCycleIndex < _cellsInRandomOrder.Count)
                            DiffuseGasAt(_cellsInRandomOrder[cellCycleIndex]);
                    }

                    cellCycleIndex++;
                }
                while (cellCycleIndex < area);
            }

            cellCycleIndex = 0;
        }

        private void DiffuseGasAt(IntVec3 cell)
        {
            int originCellIndex = CellToIndex(cell);
            int densityAtOrigin = _gasDensity[originCellIndex];
            if (densityAtOrigin < 17)
                return;

            bool diffused = false;
            GenList.Shuffle(_cardinalDirections);

            for (int i = 0; i < _cardinalDirections.Length; i++)
            {
                IntVec3 otherCell = cell + _cardinalDirections[i];
                if (!GasCanMoveTo(otherCell))
                    continue;

                int otherCellIndex = CellToIndex(otherCell);
                int densityAtOther = _gasDensity[otherCellIndex];

                if (!TryDiffuseIndividualGas(ref densityAtOrigin, ref densityAtOther))
                    continue;

                SetDirect(otherCellIndex, (byte)densityAtOther);
                _map.mapDrawer.MapMeshDirty(otherCell, MapMeshFlagDefOf.Gas);
                diffused = true;

                if (densityAtOrigin < 17)
                    break;
            }

            if (diffused)
            {
                SetDirect(originCellIndex, (byte)densityAtOrigin);
                _map.mapDrawer.MapMeshDirty(cell, MapMeshFlagDefOf.Gas);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TryDiffuseIndividualGas(ref int gasA, ref int gasB)
        {
            if (gasA < 17)
                return false;

            int diff = Mathf.Abs(gasA - gasB) >> 1;
            if (gasA <= gasB || diff < 17)
                return false;

            gasA -= diff;
            gasB += diff;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool GasCanMoveTo(IntVec3 cell)
        {
            if (!GenGrid.InBounds(cell, _map))
                return false;

            Building edifice = GridsUtility.GetEdifice(cell, _map);
            if (edifice == null)
                return true;

            if (edifice.def.Fillage != FillCategory.Full)
                return true;

            if (edifice is Building_Door door)
                return door.Open;

            return false;
        }

        public void RecalculateEverHadGas()
        {
            for (int i = _gasCoverageForIndicesInRandomOrder.Length; i-- > 0;)
            {
                if (_gasCoverageForIndicesInRandomOrder[i] != 0)
                {
                    AnyGasEverAdded = true;
                    return;
                }
            }

            AnyGasEverAdded = false;
        }
    }

    /// <summary>
    /// Fachada de alto nivel que gestiona los 4 ParallelGasGrid de cada mapa y ofrece
    /// las APIs estáticas que conectan los parches con la implementación optimizada.
    /// </summary>
    public static class GasGridOptimization
    {
        private static readonly GasInfo[] GasInfos = new[]
        {
            new GasInfo(GasType.BlindSmoke, 4, false, new Color(0.7f, 0.7f, 0.7f, 0.7f), 0),
            new GasInfo(GasType.ToxGas, 3, true, new Color(0.6f, 0.9f, 0.3f, 0.7f), 1),
            new GasInfo(GasType.RotStink, 4, true, new Color(0.8f, 0.4f, 0.2f, 0.7f), 2),
            new GasInfo(GasType.DeadlifeDust, 3, true, new Color(0.3f, 0.3f, 0.8f, 0.7f), 3),
        };

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ParallelGasGrid[] GetGrids(GasGrid grid)
        {
            ref ParallelGasGrid[] grids = ref Prepatch.ReforjedFields.ReforjedParallelGasGrids(grid);
            if (grids == null)
            {
                Map map = grid.map;
                grids = new ParallelGasGrid[4];
                for (int i = 0; i < 4; i++)
                    grids[i] = new ParallelGasGrid(map, GasInfos[i]);
            }
            return grids;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int GasTypeToIndex(GasType gasType)
        {
            switch (gasType)
            {
                case GasType.BlindSmoke: return 0;
                case GasType.ToxGas: return 1;
                case GasType.RotStink: return 2;
                case GasType.DeadlifeDust: return 3;
                default: return 0;
            }
        }

        public static void Tick(GasGrid grid)
        {
            if (!grid.CalculateGasEffects)
                return;

            ParallelGasGrid[] grids = GetGrids(grid);
            bool anyActive = false;

            for (int i = 0; i < grids.Length; i++)
            {
                ParallelGasGrid g = grids[i];
                if (g.GasInfo.IsActive())
                {
                    g.Tick();
                    if (g.AnyGasEverAdded)
                        anyActive = true;
                }
            }

            grid.anyGasEverAdded = anyActive;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool AnyGasAt(GasGrid grid, int idx)
        {
            ParallelGasGrid[] grids = GetGrids(grid);
            for (int i = 0; i < grids.Length; i++)
            {
                if (grids[i].AnyGasAt(idx))
                    return true;
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool AnyGasAt(GasGrid grid, IntVec3 cell)
        {
            return AnyGasAt(grid, CellIndicesUtility.CellToIndex(cell, grid.map.Size.x));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte DensityAt(GasGrid grid, int index, GasType gasType)
        {
            int idx = GasTypeToIndex(gasType);
            return GetGrids(grid)[idx].DensityAt(index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte DensityAt(GasGrid grid, IntVec3 cell, GasType gasType)
        {
            int idx = GasTypeToIndex(gasType);
            return GetGrids(grid)[idx].DensityAt(cell);
        }

        public static void AddGas(GasGrid grid, IntVec3 cell, GasType gasType, int amount, bool canOverflow)
        {
            int idx = GasTypeToIndex(gasType);
            GetGrids(grid)[idx].AddGas(cell, amount, canOverflow);
        }

        public static void SetDirect(GasGrid grid, int index, byte smoke, byte toxGas, byte rotStink, byte deadlifeDust)
        {
            ParallelGasGrid[] grids = GetGrids(grid);
            grids[0].SetDirect(index, smoke);
            grids[1].SetDirect(index, toxGas);
            grids[2].SetDirect(index, rotStink);
            grids[3].SetDirect(index, deadlifeDust);
        }

        public static void Debug_ClearAll(GasGrid grid)
        {
            ParallelGasGrid[] grids = GetGrids(grid);
            for (int i = 0; i < grids.Length; i++)
                grids[i].Clear();

            grid.map.mapDrawer.WholeMapChanged(MapMeshFlagDefOf.Gas);
        }

        public static void Notify_ThingSpawned(GasGrid grid, Thing thing)
        {
            if (!thing.Spawned || thing.def.Fillage != FillCategory.Full)
                return;

            ParallelGasGrid[] grids = GetGrids(grid);
            Map map = grid.map;
            int mapSizeX = map.Size.x;

            foreach (IntVec3 cell in thing.OccupiedRect())
            {
                int cellIndex = CellIndicesUtility.CellToIndex(cell, mapSizeX);
                bool dirty = false;

                for (int i = 0; i < grids.Length; i++)
                {
                    if (grids[i].AnyGasAt(cellIndex))
                    {
                        grids[i].SetDirect(cellIndex, 0);
                        dirty = true;
                    }
                }

                if (dirty)
                    map.mapDrawer.MapMeshDirty(cell, MapMeshFlagDefOf.Gas);
            }
        }
    }
}
