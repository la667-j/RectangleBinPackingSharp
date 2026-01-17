using System;
using System.Collections.Generic;

namespace RectangleBinPacking
{
    /// <summary>
    /// Implements the Guillotine Bin Packing algorithm.
    /// This algorithm works by splitting the free area into smaller free rectangles using a guillotine-style cut (edge-to-edge).
    /// It is useful for cutting operations (like glass or metal cutting) where cuts must be straight lines across the material.
    /// </summary>
    public class GuillotineBinPack
    {
        /// <summary>
        /// Specifies the heuristic used to choose the best free rectangle to place the new item.
        /// </summary>
        public enum FreeRectChoiceHeuristic
        {
            RectBestAreaFit,      // -BAF: Positions the rectangle into the smallest free rect into which it fits.
            RectBestShortSideFit, // -BSSF: Positions the rectangle against the short side of a free rectangle into which it fits the best.
            RectBestLongSideFit,  // -BLSF: Positions the rectangle against the long side of a free rectangle into which it fits the best.
            RectWorstAreaFit,     // -WAF: Positions the rectangle into the largest free rect into which it fits.
            RectWorstShortSideFit,// -WSSF: Positions the rectangle against the short side of a free rectangle into which it fits the worst.
            RectWorstLongSideFit  // -WLSF: Positions the rectangle against the long side of a free rectangle into which it fits the worst.
        }

        /// <summary>
        /// Specifies the heuristic used to decide how to split the remaining free area after placing a rectangle.
        /// </summary>
        public enum GuillotineSplitHeuristic
        {
            SplitShorterLeftoverAxis, // -SLAS: Split along the shorter leftover axis.
            SplitLongerLeftoverAxis,  // -LLAS: Split along the longer leftover axis.
            SplitMinimizeArea,        // -MINAS: Split to minimize the area of the smaller leftover rectangle.
            SplitMaximizeArea,        // -MAXAS: Split to maximize the area of the larger leftover rectangle.
            SplitShorterAxis,         // -SAS: Split along the shorter total axis.
            SplitLongerAxis           // -LAS: Split along the longer total axis.
        }

        private int binWidth;
        private int binHeight;

        /// <summary>
        /// The list of free rectangles available for packing.
        /// Exposed publicly so other algorithms (like Skyline) can use this class to manage waste maps.
        /// </summary>
        public List<Rect> FreeRectangles { get; private set; } = new List<Rect>();

        /// <summary>
        /// The list of rectangles that have been successfully packed.
        /// </summary>
        public List<Rect> UsedRectangles { get; private set; } = new List<Rect>();

        /// <summary>
        /// Initializes a new bin of the given width and height.
        /// </summary>
        public GuillotineBinPack(int width, int height) => Init(width, height);

        /// <summary>
        /// (Re)initializes the packer to an empty bin of width x height.
        /// </summary>
        public void Init(int width, int height)
        {
            binWidth = width;
            binHeight = height;
            UsedRectangles.Clear();
            FreeRectangles.Clear();
            
            // Start with a single big free rectangle covering the whole bin.
            FreeRectangles.Add(new Rect(0, 0, width, height));
        }

        /// <summary>
        /// Inserts a single rectangle into the bin.
        /// </summary>
        /// <param name="width">Width of the rectangle to insert.</param>
        /// <param name="height">Height of the rectangle to insert.</param>
        /// <param name="merge">If true, tries to merge adjacent free rectangles after placement.</param>
        /// <param name="rectChoice">Heuristic to choose which free rectangle to use.</param>
        /// <param name="splitMethod">Heuristic to decide how to split the remaining space.</param>
        /// <returns>The placed rectangle position. Returns a Rect with Height=0 if placement failed.</returns>
        public Rect Insert(int width, int height, bool merge, FreeRectChoiceHeuristic rectChoice, GuillotineSplitHeuristic splitMethod)
        {
            int index = 0;
            Rect newNode = FindPositionForNewNode(width, height, rectChoice, out index);

            // Failure to fit
            if (newNode.Height == 0) return newNode;

            // Core Logic: Split the free rectangle into new smaller free rectangles
            Rect freeNode = FreeRectangles[index];
            SplitFreeRectByHeuristic(freeNode, newNode, splitMethod);
            FreeRectangles.RemoveAt(index);

            // Optional: Merge free rectangles to reduce fragmentation
            if (merge) MergeFreeRectangles();

            UsedRectangles.Add(newNode);
            return newNode;
        }

        /// <summary>
        /// Finds the best position for the new node based on the chosen heuristic.
        /// (Simplified: Currently implements BestAreaFit and BestShortSideFit logic).
        /// </summary>
        private Rect FindPositionForNewNode(int width, int height, FreeRectChoiceHeuristic rectChoice, out int nodeIndex)
        {
            Rect bestNode = new Rect();
            int bestScore = int.MaxValue;
            nodeIndex = -1;

            for (int i = 0; i < FreeRectangles.Count; ++i)
            {
                // Check if the rectangle fits in the current free rectangle
                if (width <= FreeRectangles[i].Width && height <= FreeRectangles[i].Height)
                {
                    int score = CalculateScore(FreeRectangles[i], width, height, rectChoice);
                    if (score < bestScore)
                    {
                        nodeIndex = i;
                        bestScore = score;
                        bestNode = new Rect(FreeRectangles[i].X, FreeRectangles[i].Y, width, height);
                    }
                }
                // Note: Rotation logic is omitted here for brevity but should be added for a full implementation.
            }
            return bestNode;
        }

        /// <summary>
        /// Calculates the score for a free rectangle based on the heuristic.
        /// Lower score is better.
        /// </summary>
        private int CalculateScore(Rect free, int width, int height, FreeRectChoiceHeuristic method)
        {
            switch (method)
            {
                case FreeRectChoiceHeuristic.RectBestAreaFit: 
                    return free.Area - width * height;
                case FreeRectChoiceHeuristic.RectBestShortSideFit: 
                    return Math.Min(free.Width - width, free.Height - height);
                default: 
                    return free.Area; // Default fallback
            }
        }

        /// <summary>
        /// Splits the remaining area of a free rectangle after placing a new node into it.
        /// </summary>
        private void SplitFreeRectByHeuristic(Rect freeNode, Rect placedNode, GuillotineSplitHeuristic method)
        {
            // Calculate the dimensions of the leftover area
            int w = freeNode.Width - placedNode.Width;
            int h = freeNode.Height - placedNode.Height;

            // Decide whether to split horizontally or vertically based on the heuristic
            // Horizontal split: Cut extends horizontally from the right edge of the placed node.
            // Vertical split: Cut extends vertically from the bottom edge of the placed node.
            bool splitHorizontal = (method == GuillotineSplitHeuristic.SplitShorterLeftoverAxis && w <= h) ||
                                   (method == GuillotineSplitHeuristic.SplitLongerLeftoverAxis && w > h) ||
                                   (method == GuillotineSplitHeuristic.SplitMinimizeArea && w * placedNode.Height > h * placedNode.Width) ||
                                   (method == GuillotineSplitHeuristic.SplitMaximizeArea && w * placedNode.Height <= h * placedNode.Width); 

            if (splitHorizontal)
            {
                // Split horizontally:
                // 1. Bottom piece (full width of the original free node)
                // 2. Right piece (only to the right of the placed node)
                if (h > 0) FreeRectangles.Add(new Rect(freeNode.X, freeNode.Y + placedNode.Height, freeNode.Width, h));
                if (w > 0) FreeRectangles.Add(new Rect(freeNode.X + placedNode.Width, freeNode.Y, w, placedNode.Height));
            }
            else
            {
                // Split vertically:
                // 1. Right piece (full height of the original free node)
                // 2. Bottom piece (only below the placed node)
                if (w > 0) FreeRectangles.Add(new Rect(freeNode.X + placedNode.Width, freeNode.Y, w, freeNode.Height));
                if (h > 0) FreeRectangles.Add(new Rect(freeNode.X, freeNode.Y + placedNode.Height, placedNode.Width, h));
            }
        }

        /// <summary>
        /// Merges adjacent free rectangles into larger ones to reduce fragmentation.
        /// </summary>
        public void MergeFreeRectangles()
        {
            for (int i = 0; i < FreeRectangles.Count; ++i)
                for (int j = i + 1; j < FreeRectangles.Count; ++j)
                {
                    // Check if rectangles have the same width and are aligned horizontally
                    if (FreeRectangles[i].Width == FreeRectangles[j].Width && FreeRectangles[i].X == FreeRectangles[j].X)
                    {
                        if (FreeRectangles[i].Y == FreeRectangles[j].Bottom)
                        {
                            var r = FreeRectangles[i];
                            r.Y -= FreeRectangles[j].Height;
                            r.Height += FreeRectangles[j].Height;
                            FreeRectangles[i] = r;
                            FreeRectangles.RemoveAt(j);
                            --j;
                        }
                        else if (FreeRectangles[i].Bottom == FreeRectangles[j].Y)
                        {
                            var r = FreeRectangles[i];
                            r.Height += FreeRectangles[j].Height;
                            FreeRectangles[i] = r;
                            FreeRectangles.RemoveAt(j);
                            --j;
                        }
                    }
                    // Check if rectangles have the same height and are aligned vertically
                    else if (FreeRectangles[i].Height == FreeRectangles[j].Height && FreeRectangles[i].Y == FreeRectangles[j].Y)
                    {
                        if (FreeRectangles[i].X == FreeRectangles[j].Right)
                        {
                            var r = FreeRectangles[i];
                            r.X -= FreeRectangles[j].Width;
                            r.Width += FreeRectangles[j].Width;
                            FreeRectangles[i] = r;
                            FreeRectangles.RemoveAt(j);
                            --j;
                        }
                        else if (FreeRectangles[i].Right == FreeRectangles[j].X)
                        {
                            var r = FreeRectangles[i];
                            r.Width += FreeRectangles[j].Width;
                            FreeRectangles[i] = r;
                            FreeRectangles.RemoveAt(j);
                            --j;
                        }
                    }
                }
        }
    }
}