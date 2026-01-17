using System;
using System.Collections.Generic;

namespace RectangleBinPacking
{
    /// <summary>
    /// Specifies the heuristic rules that can be used when deciding where to place a new rectangle.
    /// </summary>
    public enum FreeRectChoiceHeuristic
    {
        /// <summary>
        /// BSSF: Positions the rectangle against the short side of a free rectangle into which it fits the best.
        /// Usually produces the best packing results.
        /// </summary>
        RectBestShortSideFit,

        /// <summary>
        /// BLSF: Positions the rectangle against the long side of a free rectangle into which it fits the best.
        /// </summary>
        RectBestLongSideFit,

        /// <summary>
        /// BAF: Positions the rectangle into the smallest free rectangle into which it fits.
        /// </summary>
        RectBestAreaFit,

        /// <summary>
        /// BL: Does the placement in "Tetris" style. Chooses the position with the lowest Y, then lowest X.
        /// </summary>
        RectBottomLeftRule,

        /// <summary>
        /// CP: Chooses the placement where the rectangle touches other packed rectangles (or bin borders) as much as possible.
        /// </summary>
        RectContactPointRule
    }

    /// <summary>
    /// Implements the MaxRects (Maximal Rectangles) bin packing algorithm.
    /// This algorithm maintains a list of all distinct free rectangular areas in the bin.
    /// When a new rectangle is placed, the algorithm calculates new free areas by splitting the existing ones.
    /// </summary>
    public class MaxRectsBinPack
    {
        public int BinWidth { get; private set; }
        public int BinHeight { get; private set; }
        public bool AllowRotations { get; private set; }

        /// <summary>
        /// Gets the list of rectangles that have been successfully packed.
        /// </summary>
        public List<Rect> UsedRectangles { get; } = new List<Rect>();

        /// <summary>
        /// Gets the list of free rectangles (the "Maximal Rectangles").
        /// Note: These rectangles often overlap.
        /// </summary>
        public List<Rect> FreeRectangles { get; } = new List<Rect>();

        /// <summary>
        /// Initializes a new instance of the MaxRectsBinPack class.
        /// </summary>
        /// <param name="width">The width of the bin.</param>
        /// <param name="height">The height of the bin.</param>
        /// <param name="allowRotations">If true, the packer is allowed to rotate rects by 90 degrees.</param>
        public MaxRectsBinPack(int width, int height, bool allowRotations = true)
        {
            Init(width, height, allowRotations);
        }

        /// <summary>
        /// (Re)initializes the packer to an empty bin of the given size.
        /// </summary>
        public void Init(int width, int height, bool allowRotations = true)
        {
            BinWidth = width;
            BinHeight = height;
            AllowRotations = allowRotations;

            UsedRectangles.Clear();
            FreeRectangles.Clear();
            
            // Start with a single big free rectangle that covers the whole bin.
            FreeRectangles.Add(new Rect(0, 0, width, height));
        }

        /// <summary>
        /// Inserts a single rectangle into the bin.
        /// </summary>
        /// <param name="width">Width of the rectangle to insert.</param>
        /// <param name="height">Height of the rectangle to insert.</param>
        /// <param name="method">The heuristic rule to use for choosing the position.</param>
        /// <returns>The placed rectangle. If the placement fails (bin full), the returned Rect has Height = 0.</returns>
        public Rect Insert(int width, int height, FreeRectChoiceHeuristic method)
        {
            Rect newNode = new Rect();
            // Used to store the best score found so far. 
            // Some heuristics use one score, others use two (primary and tie-breaker).
            int score1 = int.MaxValue;
            int score2 = int.MaxValue;

            switch (method)
            {
                case FreeRectChoiceHeuristic.RectBestShortSideFit:
                    newNode = FindPositionForNewNodeBestShortSideFit(width, height, ref score1, ref score2);
                    break;
                case FreeRectChoiceHeuristic.RectBottomLeftRule:
                    newNode = FindPositionForNewNodeBottomLeft(width, height, ref score1, ref score2);
                    break;
                case FreeRectChoiceHeuristic.RectContactPointRule:
                    newNode = FindPositionForNewNodeContactPoint(width, height, ref score1);
                    break;
                case FreeRectChoiceHeuristic.RectBestLongSideFit:
                    newNode = FindPositionForNewNodeBestLongSideFit(width, height, ref score2, ref score1);
                    break;
                case FreeRectChoiceHeuristic.RectBestAreaFit:
                    newNode = FindPositionForNewNodeBestAreaFit(width, height, ref score1, ref score2);
                    break;
            }

            // If height is 0, it means no valid position was found.
            if (newNode.Height == 0)
                return newNode;

            // Core Logic: We must split any free rectangle that intersects with the new node.
            int numRectanglesToProcess = FreeRectangles.Count;
            for (int i = 0; i < numRectanglesToProcess; ++i)
            {
                if (SplitFreeNode(FreeRectangles[i], newNode))
                {
                    FreeRectangles.RemoveAt(i);
                    --i;
                    --numRectanglesToProcess;
                }
            }

            // Pruning: Remove free rectangles that are fully contained within others to reduce memory usage.
            PruneFreeList();

            UsedRectangles.Add(newNode);
            return newNode;
        }

        // ==========================================================
        // Core Geometric Logic: Splitting Free Rectangles
        // ==========================================================

        /// <summary>
        /// Splits a free rectangle into smaller free rectangles if it intersects with the used node.
        /// </summary>
        /// <param name="freeNode">The free rectangle to split.</param>
        /// <param name="usedNode">The new rectangle being placed.</param>
        /// <returns>True if the freeNode was split (and should be removed); otherwise false.</returns>
        private bool SplitFreeNode(Rect freeNode, Rect usedNode)
        {
            // 1. If they don't intersect, no splitting is needed.
            if (!freeNode.Intersects(usedNode)) return false;

            // 2. If they intersect, the 'usedNode' punches a hole in 'freeNode'.
            // We generate up to 4 new smaller rectangles around the usedNode.
            
            // Check if UsedNode is within the horizontal range of FreeNode (Split Left/Right)
            if (usedNode.X < freeNode.Right && usedNode.Right > freeNode.X)
            {
                // New node at the top side of the used node.
                if (usedNode.Y > freeNode.Y && usedNode.Y < freeNode.Bottom)
                {
                    Rect newNode = freeNode;
                    newNode.Height = usedNode.Y - newNode.Y;
                    FreeRectangles.Add(newNode);
                }

                // New node at the bottom side of the used node.
                if (usedNode.Bottom < freeNode.Bottom)
                {
                    Rect newNode = freeNode;
                    newNode.Y = usedNode.Bottom;
                    newNode.Height = freeNode.Bottom - usedNode.Bottom;
                    FreeRectangles.Add(newNode);
                }
            }

            // Check if UsedNode is within the vertical range of FreeNode (Split Top/Bottom)
            if (usedNode.Y < freeNode.Bottom && usedNode.Bottom > freeNode.Y)
            {
                // New node at the left side of the used node.
                if (usedNode.X > freeNode.X && usedNode.X < freeNode.Right)
                {
                    Rect newNode = freeNode;
                    newNode.Width = usedNode.X - newNode.X;
                    FreeRectangles.Add(newNode);
                }

                // New node at the right side of the used node.
                if (usedNode.Right < freeNode.Right)
                {
                    Rect newNode = freeNode;
                    newNode.X = usedNode.Right;
                    newNode.Width = freeNode.Right - usedNode.Right;
                    FreeRectangles.Add(newNode);
                }
            }

            // Return true to indicate the original freeNode is now invalid and should be removed.
            return true;
        }

        /// <summary>
        /// Iterates through the free rectangle list and removes any entries that are 
        /// completely contained within another free rectangle.
        /// </summary>
        private void PruneFreeList()
        {
            for (int i = 0; i < FreeRectangles.Count; ++i)
            {
                for (int j = i + 1; j < FreeRectangles.Count; ++j)
                {
                    if (FreeRectangles[j].Contains(FreeRectangles[i]))
                    {
                        FreeRectangles.RemoveAt(i);
                        --i;
                        break;
                    }
                    if (FreeRectangles[i].Contains(FreeRectangles[j]))
                    {
                        FreeRectangles.RemoveAt(j);
                        --j;
                    }
                }
            }
        }

        // ==========================================================
        // Heuristic Implementations
        // ==========================================================

        // 1. Best Short Side Fit (BSSF) - Recommended
        private Rect FindPositionForNewNodeBestShortSideFit(int width, int height, ref int bestShortSideFit, ref int bestLongSideFit)
        {
            Rect bestNode = new Rect();
            bestShortSideFit = int.MaxValue;
            bestLongSideFit = int.MaxValue;

            for (int i = 0; i < FreeRectangles.Count; ++i)
            {
                // Try to place the rectangle in upright (non-flipped) orientation.
                if (FreeRectangles[i].Width >= width && FreeRectangles[i].Height >= height)
                {
                    int leftoverHoriz = Math.Abs(FreeRectangles[i].Width - width);
                    int leftoverVert = Math.Abs(FreeRectangles[i].Height - height);
                    int shortSideFit = Math.Min(leftoverHoriz, leftoverVert);
                    int longSideFit = Math.Max(leftoverHoriz, leftoverVert);

                    if (shortSideFit < bestShortSideFit || (shortSideFit == bestShortSideFit && longSideFit < bestLongSideFit))
                    {
                        bestNode = new Rect(FreeRectangles[i].X, FreeRectangles[i].Y, width, height);
                        bestShortSideFit = shortSideFit;
                        bestLongSideFit = longSideFit;
                    }
                }

                // Try to place the rectangle in rotated (flipped) orientation.
                if (AllowRotations && FreeRectangles[i].Width >= height && FreeRectangles[i].Height >= width)
                {
                    int flippedLeftoverHoriz = Math.Abs(FreeRectangles[i].Width - height);
                    int flippedLeftoverVert = Math.Abs(FreeRectangles[i].Height - width);
                    int flippedShortSideFit = Math.Min(flippedLeftoverHoriz, flippedLeftoverVert);
                    int flippedLongSideFit = Math.Max(flippedLeftoverHoriz, flippedLeftoverVert);

                    if (flippedShortSideFit < bestShortSideFit || (flippedShortSideFit == bestShortSideFit && flippedLongSideFit < bestLongSideFit))
                    {
                        bestNode = new Rect(FreeRectangles[i].X, FreeRectangles[i].Y, height, width);
                        bestShortSideFit = flippedShortSideFit;
                        bestLongSideFit = flippedLongSideFit;
                    }
                }
            }
            return bestNode;
        }

        // 2. Bottom Left Rule (BL)
        private Rect FindPositionForNewNodeBottomLeft(int width, int height, ref int bestY, ref int bestX)
        {
            Rect bestNode = new Rect();
            bestY = int.MaxValue;
            bestX = int.MaxValue;

            for (int i = 0; i < FreeRectangles.Count; ++i)
            {
                // Try upright
                if (FreeRectangles[i].Width >= width && FreeRectangles[i].Height >= height)
                {
                    int topSideY = FreeRectangles[i].Y + height;
                    if (topSideY < bestY || (topSideY == bestY && FreeRectangles[i].X < bestX))
                    {
                        bestNode = new Rect(FreeRectangles[i].X, FreeRectangles[i].Y, width, height);
                        bestY = topSideY;
                        bestX = FreeRectangles[i].X;
                    }
                }
                // Try rotated
                if (AllowRotations && FreeRectangles[i].Width >= height && FreeRectangles[i].Height >= width)
                {
                    int topSideY = FreeRectangles[i].Y + width;
                    if (topSideY < bestY || (topSideY == bestY && FreeRectangles[i].X < bestX))
                    {
                        bestNode = new Rect(FreeRectangles[i].X, FreeRectangles[i].Y, height, width);
                        bestY = topSideY;
                        bestX = FreeRectangles[i].X;
                    }
                }
            }
            return bestNode;
        }

        // 3. Best Area Fit (BAF)
        private Rect FindPositionForNewNodeBestAreaFit(int width, int height, ref int bestAreaFit, ref int bestShortSideFit)
        {
            Rect bestNode = new Rect();
            bestAreaFit = int.MaxValue;
            bestShortSideFit = int.MaxValue;

            for (int i = 0; i < FreeRectangles.Count; ++i)
            {
                int areaFit = FreeRectangles[i].Width * FreeRectangles[i].Height - width * height;

                // Try upright
                if (FreeRectangles[i].Width >= width && FreeRectangles[i].Height >= height)
                {
                    int leftoverHoriz = Math.Abs(FreeRectangles[i].Width - width);
                    int leftoverVert = Math.Abs(FreeRectangles[i].Height - height);
                    int shortSideFit = Math.Min(leftoverHoriz, leftoverVert);

                    if (areaFit < bestAreaFit || (areaFit == bestAreaFit && shortSideFit < bestShortSideFit))
                    {
                        bestNode = new Rect(FreeRectangles[i].X, FreeRectangles[i].Y, width, height);
                        bestShortSideFit = shortSideFit;
                        bestAreaFit = areaFit;
                    }
                }

                // Try rotated
                if (AllowRotations && FreeRectangles[i].Width >= height && FreeRectangles[i].Height >= width)
                {
                    int leftoverHoriz = Math.Abs(FreeRectangles[i].Width - height);
                    int leftoverVert = Math.Abs(FreeRectangles[i].Height - width);
                    int shortSideFit = Math.Min(leftoverHoriz, leftoverVert);

                    if (areaFit < bestAreaFit || (areaFit == bestAreaFit && shortSideFit < bestShortSideFit))
                    {
                        bestNode = new Rect(FreeRectangles[i].X, FreeRectangles[i].Y, height, width);
                        bestShortSideFit = shortSideFit;
                        bestAreaFit = areaFit;
                    }
                }
            }
            return bestNode;
        }

        // 4. Best Long Side Fit (BLSF)
        private Rect FindPositionForNewNodeBestLongSideFit(int width, int height, ref int bestShortSideFit, ref int bestLongSideFit)
        {
            Rect bestNode = new Rect();
            bestShortSideFit = int.MaxValue;
            bestLongSideFit = int.MaxValue;

            for (int i = 0; i < FreeRectangles.Count; ++i)
            {
                // Try upright
                if (FreeRectangles[i].Width >= width && FreeRectangles[i].Height >= height)
                {
                    int leftoverHoriz = Math.Abs(FreeRectangles[i].Width - width);
                    int leftoverVert = Math.Abs(FreeRectangles[i].Height - height);
                    int shortSideFit = Math.Min(leftoverHoriz, leftoverVert);
                    int longSideFit = Math.Max(leftoverHoriz, leftoverVert);

                    if (longSideFit < bestLongSideFit || (longSideFit == bestLongSideFit && shortSideFit < bestShortSideFit))
                    {
                        bestNode = new Rect(FreeRectangles[i].X, FreeRectangles[i].Y, width, height);
                        bestShortSideFit = shortSideFit;
                        bestLongSideFit = longSideFit;
                    }
                }

                // Try rotated
                if (AllowRotations && FreeRectangles[i].Width >= height && FreeRectangles[i].Height >= width)
                {
                    int leftoverHoriz = Math.Abs(FreeRectangles[i].Width - height);
                    int leftoverVert = Math.Abs(FreeRectangles[i].Height - width);
                    int shortSideFit = Math.Min(leftoverHoriz, leftoverVert);
                    int longSideFit = Math.Max(leftoverHoriz, leftoverVert);

                    if (longSideFit < bestLongSideFit || (longSideFit == bestLongSideFit && shortSideFit < bestShortSideFit))
                    {
                        bestNode = new Rect(FreeRectangles[i].X, FreeRectangles[i].Y, height, width);
                        bestShortSideFit = shortSideFit;
                        bestLongSideFit = longSideFit;
                    }
                }
            }
            return bestNode;
        }

        // 5. Contact Point Rule (CP)
        private Rect FindPositionForNewNodeContactPoint(int width, int height, ref int bestContactScore)
        {
            Rect bestNode = new Rect();
            bestContactScore = -1;

            for (int i = 0; i < FreeRectangles.Count; ++i)
            {
                // Try upright
                if (FreeRectangles[i].Width >= width && FreeRectangles[i].Height >= height)
                {
                    int score = ContactPointScoreNode(FreeRectangles[i].X, FreeRectangles[i].Y, width, height);
                    if (score > bestContactScore)
                    {
                        bestNode = new Rect(FreeRectangles[i].X, FreeRectangles[i].Y, width, height);
                        bestContactScore = score;
                    }
                }
                // Try rotated
                if (AllowRotations && FreeRectangles[i].Width >= height && FreeRectangles[i].Height >= width)
                {
                    int score = ContactPointScoreNode(FreeRectangles[i].X, FreeRectangles[i].Y, height, width);
                    if (score > bestContactScore)
                    {
                        bestNode = new Rect(FreeRectangles[i].X, FreeRectangles[i].Y, height, width);
                        bestContactScore = score;
                    }
                }
            }
            return bestNode;
        }

        /// <summary>
        /// Calculates a score based on how many edges of the new rectangle touch existing rectangles or the bin borders.
        /// </summary>
        private int ContactPointScoreNode(int x, int y, int width, int height)
        {
            int score = 0;

            // Check overlap with bin borders
            if (x == 0 || x + width == BinWidth) score += height;
            if (y == 0 || y + height == BinHeight) score += width;

            // Check overlap with existing rectangles
            foreach (var r in UsedRectangles)
            {
                // Check vertical edges
                if (r.X == x + width || r.X + r.Width == x)
                    score += CommonIntervalLength(r.Y, r.Y + r.Height, y, y + height);
                // Check horizontal edges
                if (r.Y == y + height || r.Y + r.Height == y)
                    score += CommonIntervalLength(r.X, r.X + r.Width, x, x + width);
            }
            return score;
        }

        private int CommonIntervalLength(int i1start, int i1end, int i2start, int i2end)
        {
            if (i1end < i2start || i2end < i1start) return 0;
            return Math.Min(i1end, i2end) - Math.Max(i1start, i2start);
        }
    }
}