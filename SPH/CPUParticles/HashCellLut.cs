using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

namespace MyMathUntility
{
    public class HashCellLut
    {
        public delegate void ProcessFunc(int result);
        struct LutNode
        {
            public uint cellkey;
            public int index;
            public LutNode (int index, uint key)
            {
                cellkey = key;
                this.index = index;
            }
        }
        private float radius;
        private LutNode[] spatialLut;
        private int[] startIndices;
        private int length;
        private (int, int)[] celloffsets;
        public HashCellLut(int length)
        {
            this.length = length;
            spatialLut = new LutNode[length];
            startIndices = new int[length];
            celloffsets = new (int,int)[9];
            celloffsets[0] = (-1,-1);
            celloffsets[1] = (-1, 0);
            celloffsets[2] = (-1, 1);
            celloffsets[3] = (0, -1);
            celloffsets[4] = (0, 1);
            celloffsets[5] = (1, 1);
            celloffsets[6] = (1,-1);
            celloffsets[7] = (1, 0);
            celloffsets[8] = (0, 0);
        }

        public void StartUpdateLut(float radius)
        {
            this.radius = radius;
        }
        public void UpdateSpatialLookup(Vector2 point, int index)
        {
            (int cellX, int cellY) = PositionToCellcoord(point, radius);
            uint cellKey = GetKeyFromCellcoord(cellX, cellY);
            spatialLut[index] = new LutNode(index, cellKey);
            startIndices[index] = int.MaxValue;
        }
        public void FinishUpdateLut()
        {
            System.Array.Sort(spatialLut, (x, y) => x.cellkey.CompareTo(y.cellkey));
            // Calculate start indices of each unique cell key in the spatial lookuptable
            uint keyPrev = uint.MaxValue;
            for(int i = 0; i < length; ++i)
            {
                uint key = spatialLut[i].cellkey;
                if(key != keyPrev)
                {
                    startIndices[key] = i;
                    keyPrev = key;
                }
            }
        }
        private (int x, int y) PositionToCellcoord(Vector2 point, float radius)
        {
            int cellX = (int)(point.x / radius);
            int cellY = (int)(point.y / radius);
            return (cellX, cellY);
        }
        private uint GetKeyFromCellcoord(int cellX, int cellY)
        {
            uint a = (uint)cellX * 15823;
            uint b = (uint)cellY * 9737;
            return (a + b) % (uint)length;
        }
        public void GetPointsWithinRadius(Vector2 samplePoint, ProcessFunc processFunc)
        {
            // Find which cell the sample point is in (this will be the centre of our 3x3 block)
            (int centreX, int centreY) = PositionToCellcoord(samplePoint, radius);

            // Loop over all cells of the 3x3 block around the centre cell
            foreach((int offsetX,int offsetY) in celloffsets)
            {
                // Get key of current cell, then loop over all points that share that key
                uint key = GetKeyFromCellcoord(centreX + offsetX, centreY + offsetY);
                int startIndex = startIndices[key];
                for(int i = startIndex; i < spatialLut.Length; i++)
                {
                    // Exit loop if we're no longer looking at the correct cell
                    if(spatialLut[i].cellkey != key) break;
                    int index = spatialLut[i].index;
                    processFunc(index);
                }
            }
        }
    }
}
