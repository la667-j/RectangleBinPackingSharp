using System;
using System.Collections.Generic;

namespace RectangleBinPacking
{
    /// <summary>
    /// Implements bin packing algorithms that use the SKYLINE data structure to store the bin contents. 
    /// This algorithm tracks the "horizon" (skyline) of the packed rectangles.
    /// It can optionally use a GuillotineBinPack to manage and recover "waste" areas (gaps) created below the skyline.
    /// </summary>
    public class SkylineBinPack
    {
        /// <summary>
        /// Defines the different heuristic rules that can be used to decide how to make the rectangle placements.
        /// </summary>
        public enum LevelChoiceHeuristic
        {
            /// <summary>
            /// -BL: Positions the rectangle at the lowest possible level (y-coordinate), and then the left-most position (x-coordinate).
            /// </summary>
            LevelBottomLeft,

            /// <summary>
            /// -MWF: Positions the rectangle where it causes the minimum amount of "wasted" area (gaps) below the new rectangle.
            /// </summary>
            LevelMinWasteFit
        }

        private int binWidth;
        private int binHeight;
        
        // Tracks the total used surface area.
        private ulong usedSurfaceArea; 
        
        // The list of nodes defining the current skyline.
        private List<SkylineNode> skyLine = new List<SkylineNode>();
        
        // Waste map settings
        private bool useWasteMap;
        private GuillotineBinPack wasteMap;

        /// <summary>
        /// Represents a single horizontal segment of the skyline.
        /// </summary>
        private struct SkylineNode
        {
            /// <summary>The x-coordinate of the start of this segment.</summary>
            public int X;
            /// <summary>The y-coordinate (height) of this segment.</summary>
            public int Y;
            /// <summary>The width of this segment.</summary>
            public int Width;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SkylineBinPack"/> class.
        /// </summary>
        /// <param name="width">The width of the bin.</param>
        /// <param name="height">The height of the bin.</param>
        /// <param name="useWasteMap">If true, uses a GuillotineBinPack to recover wasted areas.</param>
        public SkylineBinPack(int width, int height, bool useWasteMap)
        {
            Init(width, height, useWasteMap);
        }

        /// <summary>
        /// (Re)initializes the packer to an empty bin of the given size.
        /// </summary>
        public void Init(int width, int height, bool useWasteMap)
        {
            binWidth = width;
            binHeight = height;
            this.useWasteMap = useWasteMap;
            usedSurfaceArea = 0;

            skyLine.Clear();
            // Initial state: A single flat line covering the entire width at height 0.
            skyLine.Add(new SkylineNode { X = 0, Y = 0, Width = binWidth });

            if (useWasteMap)
            {
                wasteMap = new GuillotineBinPack(width, height);
                // Clear the default free rect in Guillotine since it's only used for waste chunks
                wasteMap.FreeRectangles.Clear(); 
            }
        }

        /// <summary>
        /// Inserts a single rectangle into the bin.
        /// </summary>
        /// <param name="width">The width of the rectangle.</param>
        /// <param name="height">The height of the rectangle.</param>
        /// <param name="method">The heuristic rule to use for choosing the position.</param>
        /// <returns>The placed rectangle. Returns a Rect with Height=0 if placement failed.</returns>
        public Rect Insert(int width, int height, LevelChoiceHeuristic method)
        {
            // 1. First try to pack into the waste map (if enabled)
            if (useWasteMap)
            {
                // We use BestShortSideFit and SplitMaximizeArea for the waste map as per original implementation
                Rect wasteNode = wasteMap.Insert(width, height, true, 
                    GuillotineBinPack.FreeRectChoiceHeuristic.RectBestShortSideFit, 
                    GuillotineBinPack.GuillotineSplitHeuristic.SplitMaximizeArea);
                
                if (wasteNode.Height != 0) // Fits in waste map
                {
                    usedSurfaceArea += (ulong)width * (ulong)height;
                    return wasteNode;
                }
            }

            // 2. Use Skyline heuristics to find a position
            Rect newNode = new Rect();
            switch (method)
            {
                case LevelChoiceHeuristic.LevelBottomLeft: 
                    newNode = InsertBottomLeft(width, height); 
                    break;
                case LevelChoiceHeuristic.LevelMinWasteFit: 
                    newNode = InsertMinWaste(width, height); 
                    break;
            }
            return newNode;
        }

        private Rect InsertBottomLeft(int width, int height)
        {
            int bestHeight, bestWidth, bestIndex;
            Rect newNode = FindPositionForNewNodeBottomLeft(width, height, out bestHeight, out bestWidth, out bestIndex);

            if (bestIndex != -1)
            {
                AddSkylineLevel(bestIndex, newNode);
                usedSurfaceArea += (ulong)width * (ulong)height;
            }
            else
            {
                newNode = new Rect(); // Fail
            }

            return newNode;
        }

        private Rect InsertMinWaste(int width, int height)
        {
            int bestHeight, bestWastedArea, bestIndex;
            Rect newNode = FindPositionForNewNodeMinWaste(width, height, out bestHeight, out bestWastedArea, out bestIndex);

            if (bestIndex != -1)
            {
                AddSkylineLevel(bestIndex, newNode);
                usedSurfaceArea += (ulong)width * (ulong)height;
            }
            else
            {
                newNode = new Rect();
            }

            return newNode;
        }

        private Rect FindPositionForNewNodeBottomLeft(int width, int height, out int bestHeight, out int bestWidth, out int bestIndex)
        {
            bestHeight = int.MaxValue;
            bestWidth = int.MaxValue;
            bestIndex = -1;
            Rect newNode = new Rect();

            for (int i = 0; i < skyLine.Count; ++i)
            {
                int y;
                // Try upright
                if (RectangleFits(i, width, height, out y))
                {
                    if (y + height < bestHeight || (y + height == bestHeight && skyLine[i].Width < bestWidth))
                    {
                        bestHeight = y + height;
                        bestIndex = i;
                        bestWidth = skyLine[i].Width;
                        newNode.X = skyLine[i].X;
                        newNode.Y = y;
                        newNode.Width = width;
                        newNode.Height = height;
                    }
                }
                // Try rotated
                if (RectangleFits(i, height, width, out y))
                {
                    if (y + width < bestHeight || (y + width == bestHeight && skyLine[i].Width < bestWidth))
                    {
                        bestHeight = y + width;
                        bestIndex = i;
                        bestWidth = skyLine[i].Width;
                        newNode.X = skyLine[i].X;
                        newNode.Y = y;
                        newNode.Width = height;
                        newNode.Height = width;
                    }
                }
            }
            return newNode;
        }

        private Rect FindPositionForNewNodeMinWaste(int width, int height, out int bestHeight, out int bestWastedArea, out int bestIndex)
        {
            bestHeight = int.MaxValue;
            bestWastedArea = int.MaxValue;
            bestIndex = -1;
            Rect newNode = new Rect();

            for (int i = 0; i < skyLine.Count; ++i)
            {
                int y, wastedArea;
                // Try upright
                if (RectangleFits(i, width, height, out y, out wastedArea))
                {
                    if (wastedArea < bestWastedArea || (wastedArea == bestWastedArea && y + height < bestHeight))
                    {
                        bestHeight = y + height;
                        bestWastedArea = wastedArea;
                        bestIndex = i;
                        newNode.X = skyLine[i].X; newNode.Y = y; newNode.Width = width; newNode.Height = height;
                    }
                }
                // Try rotated
                if (RectangleFits(i, height, width, out y, out wastedArea))
                {
                    if (wastedArea < bestWastedArea || (wastedArea == bestWastedArea && y + width < bestHeight))
                    {
                        bestHeight = y + width;
                        bestWastedArea = wastedArea;
                        bestIndex = i;
                        newNode.X = skyLine[i].X; newNode.Y = y; newNode.Width = height; newNode.Height = width;
                    }
                }
            }
            return newNode;
        }

        /// <summary>
        /// Checks if a rectangle fits at the given skyline node.
        /// </summary>
        private bool RectangleFits(int skylineNodeIndex, int width, int height, out int y)
        {
            int x = skyLine[skylineNodeIndex].X;
            if (x + width > binWidth) { y = -1; return false; }
            
            int widthLeft = width;
            int i = skylineNodeIndex;
            y = skyLine[skylineNodeIndex].Y;
            
            // Scan through skyline nodes to find the max Y (ceiling) required
            while (widthLeft > 0)
            {
                y = Math.Max(y, skyLine[i].Y);
                if (y + height > binHeight) return false;
                widthLeft -= skyLine[i].Width;
                ++i;
                if (i >= skyLine.Count && widthLeft > 0) return false; // Prevent out of bounds
            }
            return true;
        }

        private bool RectangleFits(int skylineNodeIndex, int width, int height, out int y, out int wastedArea)
        {
            bool fits = RectangleFits(skylineNodeIndex, width, height, out y);
            if (fits)
                wastedArea = ComputeWastedArea(skylineNodeIndex, width, height, y);
            else 
                wastedArea = 0;
            return fits;
        }

        /// <summary>
        /// Computes the area of "gaps" (wasted space) below the rectangle if placed at this position.
        /// </summary>
        private int ComputeWastedArea(int skylineNodeIndex, int width, int height, int y)
        {
            int wastedArea = 0;
            int rectLeft = skyLine[skylineNodeIndex].X;
            int rectRight = rectLeft + width;
            
            for (; skylineNodeIndex < skyLine.Count && skyLine[skylineNodeIndex].X < rectRight; ++skylineNodeIndex)
            {
                if (skyLine[skylineNodeIndex].X >= rectRight || skyLine[skylineNodeIndex].X + skyLine[skylineNodeIndex].Width <= rectLeft)
                    break;

                int leftSide = skyLine[skylineNodeIndex].X;
                int rightSide = Math.Min(rectRight, leftSide + skyLine[skylineNodeIndex].Width);
                
                // Add area between the rectangle bottom (y) and the skyline level
                wastedArea += (rightSide - leftSide) * (y - skyLine[skylineNodeIndex].Y);
            }
            return wastedArea;
        }

        /// <summary>
        /// Updates the skyline structure after a rectangle has been placed.
        /// </summary>
        private void AddSkylineLevel(int skylineNodeIndex, Rect rect)
        {
            // If waste map is enabled, track the gaps below this new level
            if (useWasteMap)
                AddWasteMapArea(skylineNodeIndex, rect.Width, rect.Height, rect.Y);

            // Insert new skyline node representing the top of the new rectangle
            SkylineNode newNode = new SkylineNode { X = rect.X, Y = rect.Y + rect.Height, Width = rect.Width };
            skyLine.Insert(skylineNodeIndex, newNode);

            // Check adjacent nodes to handle overlaps (shortening or removing covered nodes)
            for (int i = skylineNodeIndex + 1; i < skyLine.Count; ++i)
            {
                // The skyline nodes are sorted by X coordinate
                
                // If the new node covers the start of the next node
                if (skyLine[i].X < skyLine[i - 1].X + skyLine[i - 1].Width)
                {
                    int shrink = skyLine[i - 1].X + skyLine[i - 1].Width - skyLine[i].X;
                    
                    // Structs are value types, so we must copy, modify, and assign back
                    var node = skyLine[i];
                    node.X += shrink;
                    node.Width -= shrink;
                    skyLine[i] = node;

                    if (skyLine[i].Width <= 0)
                    {
                        skyLine.RemoveAt(i);
                        --i;
                    }
                    else
                    {
                        // Node was shrunk but still exists; subsequent nodes are safe
                        break;
                    }
                }
                else
                {
                    break;
                }
            }
            MergeSkylines();
        }

        /// <summary>
        /// Adds the wasted area (gaps) below the new rectangle into the waste map for later recovery.
        /// </summary>
        private void AddWasteMapArea(int skylineNodeIndex, int width, int height, int y)
        {
            int rectLeft = skyLine[skylineNodeIndex].X;
            int rectRight = rectLeft + width;
            
            for (; skylineNodeIndex < skyLine.Count && skyLine[skylineNodeIndex].X < rectRight; ++skylineNodeIndex)
            {
                int leftSide = skyLine[skylineNodeIndex].X;
                int rightSide = Math.Min(rectRight, leftSide + skyLine[skylineNodeIndex].Width);
                
                // The area below the new rect and above the current skyline level is "waste"
                Rect waste = new Rect(leftSide, skyLine[skylineNodeIndex].Y, rightSide - leftSide, y - skyLine[skylineNodeIndex].Y);
                if (waste.Area > 0)
                {
                    // Ensure the Guillotine class's FreeRectangles property is accessible
                    wasteMap.FreeRectangles.Add(waste);
                }
            }
        }

        /// <summary>
        /// Merges adjacent skyline nodes that are at the same level (Y-coordinate).
        /// </summary>
        private void MergeSkylines()
        {
            for (int i = 0; i < skyLine.Count - 1; ++i)
            {
                if (skyLine[i].Y == skyLine[i + 1].Y)
                {
                    var node = skyLine[i];
                    node.Width += skyLine[i + 1].Width;
                    skyLine[i] = node;
                    skyLine.RemoveAt(i + 1);
                    --i;
                }
            }
        }
    }
}