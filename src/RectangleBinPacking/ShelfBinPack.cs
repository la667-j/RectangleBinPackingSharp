using System;
using System.Collections.Generic;
using System.Linq;

namespace RectangleBinPacking
{
    /// <summary>
    /// Implements the Shelf Bin Packing algorithm.
    /// This algorithm organizes rectangles into horizontal rows (shelves). 
    /// The "waste map" functionality can be enabled to use a Guillotine packer to recover lost space in the background.
    /// </summary>
    public class ShelfBinPack
    {
        /// <summary>
        /// Defines the heuristic rules used to decide which shelf to place a new rectangle into.
        /// </summary>
        public enum ShelfChoiceHeuristic
        {
            /// <summary>
            /// -NF: We always put the new rectangle to the last open shelf. 
            /// This is the fastest strategy but usually packs poorly.
            /// </summary>
            ShelfNextFit,

            /// <summary>
            /// -FF: We test the new rectangle against each shelf in turn and pack it to the first one where it fits.
            /// </summary>
            ShelfFirstFit,

            /// <summary>
            /// -BAF: Choose the shelf with the smallest remaining shelf area.
            /// </summary>
            ShelfBestAreaFit,

            /// <summary>
            /// -WAF: Choose the shelf with the largest remaining shelf area.
            /// </summary>
            ShelfWorstAreaFit,

            /// <summary>
            /// -BHF: Choose the smallest shelf (height-wise) where the rectangle fits.
            /// </summary>
            ShelfBestHeightFit,

            /// <summary>
            /// -BWF: Choose the shelf that has the least remaining horizontal shelf space available after packing.
            /// </summary>
            ShelfBestWidthFit,

            /// <summary>
            /// -WWF: Choose the shelf that will have the most remaining horizontal shelf space available after packing.
            /// </summary>
            ShelfWorstWidthFit
        }

        private int binWidth;
        private int binHeight;
        private int currentY;
        private List<Shelf> shelves = new List<Shelf>();
        private bool useWasteMap;
        private GuillotineBinPack wasteMap;

        /// <summary>
        /// Represents a horizontal slab of space where rectangles may be placed.
        /// </summary>
        private class Shelf
        {
            /// <summary>
            /// The x-coordinate that specifies where the used shelf space ends.
            /// </summary>
            public int CurrentX;

            /// <summary>
            /// The y-coordinate where this shelf starts.
            /// </summary>
            public int StartY;

            /// <summary>
            /// The height of this shelf. The topmost shelf is "open" and its height may grow.
            /// </summary>
            public int Height;

            /// <summary>
            /// Tracks rectangles placed on this shelf. Used for waste map calculation when the shelf is closed.
            /// </summary>
            public List<Rect> UsedRectangles = new List<Rect>();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ShelfBinPack"/> class.
        /// </summary>
        /// <param name="width">The width of the bin.</param>
        /// <param name="height">The height of the bin.</param>
        /// <param name="useWasteMap">If true, a GuillotineBinPack is used in the background to recover wasted space.</param>
        public ShelfBinPack(int width, int height, bool useWasteMap) => Init(width, height, useWasteMap);

        /// <summary>
        /// (Re)initializes the packer to an empty bin of the given size.
        /// </summary>
        public void Init(int width, int height, bool useWasteMap)
        {
            binWidth = width;
            binHeight = height;
            this.useWasteMap = useWasteMap;
            currentY = 0;
            shelves.Clear();
            StartNewShelf(0);

            if (useWasteMap)
            {
                // Guillotine packer used for waste recovery
                wasteMap = new GuillotineBinPack(width, height);
                wasteMap.FreeRectangles.Clear();
            }
        }

        /// <summary>
        /// Inserts a single rectangle into the bin.
        /// </summary>
        /// <param name="width">The width of the rectangle.</param>
        /// <param name="height">The height of the rectangle.</param>
        /// <param name="method">The heuristic rule to use for choosing a shelf.</param>
        /// <returns>The position of the placed rectangle. Returns a Rect with Height=0 if it failed to pack.</returns>
        public Rect Insert(int width, int height, ShelfChoiceHeuristic method)
        {
            Rect newNode = new Rect();

            // 1. Try to pack into the waste map first (if enabled)
            // This recycles the empty space left above shorter rectangles in previous shelves.
            if (useWasteMap)
            {
                newNode = wasteMap.Insert(width, height, true, 
                    GuillotineBinPack.FreeRectChoiceHeuristic.RectBestShortSideFit, 
                    GuillotineBinPack.GuillotineSplitHeuristic.SplitMaximizeArea);
                
                if (newNode.Height != 0) return newNode;
            }

            // 2. Try to fit on existing shelves
            switch (method)
            {
                case ShelfChoiceHeuristic.ShelfNextFit:
                    // Only check the latest shelf (Topmost)
                    if (FitsOnShelf(shelves.Last(), width, height, true)) 
                    { 
                        AddToShelf(shelves.Last(), width, height, ref newNode); 
                        return newNode; 
                    }
                    break;
                
                case ShelfChoiceHeuristic.ShelfFirstFit:
                    // Check all shelves, starting from the bottom
                    foreach (var shelf in shelves)
                    {
                        // Can resize only if it's the last (topmost) shelf
                        bool isLastShelf = shelf == shelves.Last();
                        if (FitsOnShelf(shelf, width, height, isLastShelf)) 
                        { 
                            AddToShelf(shelf, width, height, ref newNode); 
                            return newNode; 
                        }
                    }
                    break;
                
                // Note: Other heuristics (BestAreaFit, etc.) would be implemented here similarly 
                // by iterating all shelves and scoring them.
            }

            // 3. Start a new shelf
            // If we didn't fit on any existing shelf, we must open a new one.
            
            // Flip the rectangle so that the long side is horizontal to minimize the new shelf height.
            if (width < height && height <= binWidth) Swap(ref width, ref height);

            if (CanStartNewShelf(height))
            {
                // If waste map is enabled, move the remaining area of the current shelf to the waste map
                // before opening a new one.
                if (useWasteMap) MoveShelfToWasteMap(shelves.Last());
                
                StartNewShelf(height);
                AddToShelf(shelves.Last(), width, height, ref newNode);
                return newNode;
            }

            // Failed to pack
            return newNode;
        }

        /// <summary>
        /// Checks if a rectangle fits on the given shelf.
        /// </summary>
        private bool FitsOnShelf(Shelf shelf, int width, int height, bool canResize)
        {
            // If the shelf is the topmost one (canResize=true), we can theoretically extend height up to the bin top.
            int shelfHeight = canResize ? (binHeight - shelf.StartY) : shelf.Height;
            
            // Check fits in upright or rotated orientation
            return (shelf.CurrentX + width <= binWidth && height <= shelfHeight) || 
                   (shelf.CurrentX + height <= binWidth && width <= shelfHeight);
        }

        /// <summary>
        /// Adds the rectangle to the specific shelf, updating the shelf's dimensions.
        /// </summary>
        private void AddToShelf(Shelf shelf, int width, int height, ref Rect newNode)
        {
            // Auto-rotate logic:
            // If the width is greater than height, but placing it upright fits better horizontally?
            // (Simplified rotation logic from C++ original)
            if ((width > height && width > binWidth - shelf.CurrentX) || (width > height && width < shelf.Height)) 
                Swap(ref width, ref height); 

            newNode.X = shelf.CurrentX;
            newNode.Y = shelf.StartY;
            newNode.Width = width;
            newNode.Height = height;
            
            shelf.UsedRectangles.Add(newNode);
            
            // Advance X position
            shelf.CurrentX += width;
            
            // Grow shelf height if needed (only affects the topmost open shelf)
            shelf.Height = Math.Max(shelf.Height, height);
        }

        private bool CanStartNewShelf(int height) => shelves.Last().StartY + shelves.Last().Height + height <= binHeight;
        
        private void StartNewShelf(int startHeight)
        {
            if (shelves.Count > 0) 
                currentY += shelves.Last().Height;
            
            shelves.Add(new Shelf { CurrentX = 0, StartY = currentY, Height = startHeight });
        }

        /// <summary>
        /// Moves the free space remaining on a closed shelf into the waste map (Guillotine packer).
        /// </summary>
        private void MoveShelfToWasteMap(Shelf shelf)
        {
            // 1. Add the gaps between each rect top and shelf ceiling to the waste map.
            foreach (var r in shelf.UsedRectangles)
            {
                Rect waste = new Rect(r.X, r.Y + r.Height, r.Width, shelf.Height - r.Height);
                if (waste.Height > 0) 
                    wasteMap.FreeRectangles.Add(waste);
            }
            
            // 2. Add the space after the shelf end (right side of the last rect).
            Rect rightWaste = new Rect(shelf.CurrentX, shelf.StartY, binWidth - shelf.CurrentX, shelf.Height);
            if (rightWaste.Width > 0) 
                wasteMap.FreeRectangles.Add(rightWaste);
            
            // Mark shelf as fully used/closed
            shelf.CurrentX = binWidth; 
            
            // Merge adjacent free rectangles in the waste map to improve future fits
            wasteMap.MergeFreeRectangles();
        }

        private void Swap(ref int a, ref int b) { int t = a; a = b; b = t; }
    }
}