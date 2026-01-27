using System;
using System.Collections.Generic;
using System.Linq;

namespace RectangleBinPacking
{
    /// <summary>
    /// Implements a specific algorithm for packing a single type of rectangle (multiple copies) 
    /// optimized for height and efficiency (Single Part Nesting).
    /// </summary>
    public class SingleBinPack
    {
        private int binWidth;
        private int binHeight;
        
        // Proportional factor used in original code, default is 0.5, can be adjusted as needed.
        private double prop = 0.5; 
        private int thresholdH;
        private int thresholdV;

        public SingleBinPack(int width, int height)
        {
            this.binWidth = width;
            this.binHeight = height;
        }

        /// <summary>
        /// Executes single part nesting.
        /// </summary>
        /// <param name="partWidth">Part width.</param>
        /// <param name="partHeight">Part height.</param>
        /// <param name="quantity">Quantity.</param>
        /// <returns>List of packing results.</returns>
        public List<Rect> Insert(int partWidth, int partHeight, int quantity)
        {
            // Construct PartItem to adapt to the original algorithm logic
            var itemsToPack = new List<PartItem>();
            for (int i = 0; i < quantity; i++)
            {
                itemsToPack.Add(new PartItem
                {
                    SourceKey = "Item",
                    Width = partWidth,
                    Height = partHeight,
                    // Simulate original Part object
                    OriginalPart = new MockPart(0, 0, partWidth, partHeight),
                    OriginalPartRot = new MockPart(0, 0, partHeight, partWidth) 
                });
            }

            var solution = SolveSingleTypeHeightOptimized(itemsToPack);

            // Convert internal NestingSolution to library-standard List<Rect>
            var result = new List<Rect>();
            if (solution == null || solution.Solution == null) return result;

            foreach (var kvp in solution.Solution)
            {
                foreach (var rotKvp in kvp.Value)
                {
                    bool isRotated = !GeometryUtil.AlmostEqual(0, rotKvp.Key);
                    int currentW = isRotated ? partHeight : partWidth;
                    int currentH = isRotated ? partWidth : partHeight;

                    foreach (var offset in rotKvp.Value)
                    {
                        result.Add(new Rect((int)offset.X, (int)offset.Y, currentW, currentH));
                    }
                }
            }

            return result;
        }

        // =======================================================================
        // Core Algorithm Logic
        // =======================================================================

        private NestingSolution SolveSingleTypeHeightOptimized(List<PartItem> items)
        {
            if (items == null || items.Count == 0) return null;
            var template = items[0];
            int totalCount = items.Count;
            int sheetH = this.binHeight; // Adapt: use binHeight
            var sheetW = this.binWidth;  // Adapt: use binWidth
            bool isOriginalVertical = template.Height > template.Width;

            // Vertical parameters (used below)
            int wVert = isOriginalVertical ? template.Width : template.Height;
            int hVert = isOriginalVertical ? template.Height : template.Width;
            thresholdH = (int)(hVert * prop);
            thresholdV = (int)(wVert * prop);
            double angleVert = isOriginalVertical ? 0.0 : 90.0;
            double angleHorz = isOriginalVertical ? 90.0 : 0.0;
            var maxRowV = sheetH / wVert; // Calculate max rows for horizontal placement
            var maxRowH = sheetH / hVert; // Calculate max rows for vertical placement
            
            // Adapt: MockPart MinX/MinY default to 0
            var offsetOrigion = new Vector2d(-template.OriginalPart.MinX, -template.OriginalPart.MinY);
            var offsetOrigionRot = new Vector2d(-template.OriginalPartRot.MinX, -template.OriginalPartRot.MinY);
            var offset_H = isOriginalVertical ? offsetOrigionRot : offsetOrigion; // Horizontal
            var offset_V = isOriginalVertical ? offsetOrigion : offsetOrigionRot; // Vertical is opposite to horizontal

            // Record improvement history
            var history = new List<NestingSolution>();
            NestingSolution bestSolution = null;

            // 1. All horizontal
            var nestingSolution_H = SinglePartH(maxRowV, maxRowH, sheetH, sheetW, wVert, hVert,
                template, offset_H, offset_V,
                totalCount, angleHorz, angleVert);
            
            bestSolution = nestingSolution_H;
            history.Add(bestSolution.Clone());

            // 2. All vertical (if feasible)
            if (maxRowH > 0)
            {
                var nestingSolution_V = SinglePartV(maxRowV, maxRowH, sheetH, sheetW, wVert, hVert,
                    template, offset_H, offset_V,
                    totalCount, angleHorz, angleVert);

                // If V is better than CurrentBest (H), update
                if (IsBetterSolution(nestingSolution_V, bestSolution))
                {
                    bestSolution = nestingSolution_V;
                    history.Add(bestSolution.Clone());
                }

                if (wVert + hVert > sheetH)
                {
                    // Both exceed limits, invalid result, return result
                    // Comparison between V and H done, return bestSolution directly
                    bestSolution.HistorySolutions = new List<NestingSolution>(history);
                    return bestSolution;
                }

                // 3. Mixed packing
                var curBNestingSolution = SinglePartMixHV(maxRowV, maxRowH, sheetH, sheetW, wVert, hVert,
                    template, offset_H, offset_V,
                    totalCount, angleHorz, angleVert, true);
                
                // If Mix is better than CurrentBest, update
                if (IsBetterSolution(curBNestingSolution, bestSolution))
                {
                    bestSolution = curBNestingSolution;
                    history.Add(bestSolution.Clone());
                }

                bestSolution.HistorySolutions = new List<NestingSolution>(history);
                return bestSolution;
            }

            bestSolution.HistorySolutions = new List<NestingSolution>(history);
            return bestSolution;
        }

        private NestingSolution SinglePartH(
            int maxRowV, int maxRowH, int sheetH, int sheetW, int wVert, int hVert,
            PartItem template, Vector2d offset_H, Vector2d offset_V,
            int totalCount, double angleHorz, double angleVert)
        {
            // 1. Try all horizontal (except the last column, which might be adjusted)
            Dictionary<double, List<Vector2d>> offsetsH = new Dictionary<double, List<Vector2d>>();
            var maxX_H = 0;
            var outerContourPoint_H = new OuterContourPoint(sheetH);
            
            // Protection against division by zero
            if (maxRowV == 0) return new NestingSolution(new Dictionary<string, Dictionary<double, List<Vector2d>>>(), 0, 0, outerContourPoint_H);

            if (totalCount <= maxRowV)
            {
                for (int i = 0; i < totalCount; i++)
                {
                    TryAddOffset(offsetsH, angleHorz, new Vector2d(0 + offset_H.X, i * wVert + offset_H.Y));
                }

                maxX_H = hVert;
                outerContourPoint_H.Update(0, maxX_H, totalCount * wVert);
            }
            else
            {
                var maxCol = Math.Min((totalCount + maxRowV - 1) / maxRowV - 1, sheetW / hVert - 1); // Calculate max columns - 1
                if (maxCol < 0) maxCol = 0;

                for (int j = 0; j < maxCol; j++)
                {
                    for (int i = 0; i < maxRowV; i++)
                    {
                        TryAddOffset(offsetsH, angleHorz, new Vector2d(j * hVert + offset_H.X, i * wVert + offset_H.Y));
                    }
                }

                int reaminNum = totalCount - offsetsH.Sum(o => o.Value.Count);
                int lastColNum = Math.Min(reaminNum, maxRowV);
                if (maxRowH <= 0)
                {
                    // Cannot fit vertically, directly add horizontal results
                    for (int i = 0; i < lastColNum; i++)
                    {
                        TryAddOffset(offsetsH, angleHorz, new Vector2d(maxCol * hVert + offset_H.X, i * wVert + offset_H.Y));
                    }

                    maxX_H = (maxCol + 1) * hVert;
                    outerContourPoint_H.Update(0, maxX_H, lastColNum * wVert);
                }
                else
                {
                    outerContourPoint_H.Update(0, maxCol * hVert, maxRowV * wVert);
                    // Three cases: 1. All vertical, 2. All horizontal, 3. Mixed. Choose the one with max quantity and min maxX
                    // 1. All vertical
                    int col1_theory = (sheetW - maxCol * hVert) / wVert; // Theoretical max columns
                    int num1_theory = col1_theory * maxRowH; // Theoretical max quantity
                    int num1 = 0;
                    int maxX1 = maxCol * hVert;
                    if (reaminNum < num1_theory)
                    {
                        // Less than max quantity, place all
                        int remainNum1 = reaminNum;
                        for (int i = 0; i < col1_theory && remainNum1 > 0; i++)
                        {
                            for (int j = 0; j < maxRowH && remainNum1 > 0; j++)
                            {
                                remainNum1--;
                            }

                            maxX1 += wVert;
                        }

                        num1 = reaminNum;
                    }
                    else
                    {
                        // Greater than or equal to max quantity, take limit value
                        num1 = num1_theory;
                        maxX1 = maxCol * hVert + col1_theory * wVert;
                    }

                    // 2. All horizontal
                    int num2 = lastColNum; // Special case: can place vertically after horizontal, cases 2 & 3 degenerate into one
                    int maxX2 = (maxCol + 1) * hVert;
                    bool merge3 = false;
                    if (reaminNum > lastColNum)
                    {
                        // Merge case 3, check if vertical placement is possible
                        int reaminWidth = sheetW - (maxCol + 1) * hVert;
                        int reaminWidthCol = reaminWidth / wVert; // Columns that can still be placed vertically
                        if (reaminWidthCol > 0)
                        {
                            merge3 = true;
                            int reaminWidthColNum = reaminWidthCol * maxRowH; // Theoretical limit value
                            int remainNum3 = reaminNum - lastColNum; // Actual remaining value
                            if (reaminWidthColNum >= remainNum3)
                            {
                                // Place all remaining
                                int col3 = (remainNum3 + maxRowH - 1) / maxRowH; // Actual max columns
                                num2 += remainNum3;
                                maxX2 += col3 * wVert;
                            }
                            else
                            {
                                // Still cannot fit
                                num2 += reaminWidthColNum;
                                maxX2 += reaminWidthCol * wVert;
                            }
                        }
                    }

                    if (num1 > num2 || (num1 == num2 && maxX1 < maxX2))
                    {
                        // Choose option 1
                        maxX_H = maxX1;
                        if (num1 == num1_theory)
                        {
                            // Take limit value
                            outerContourPoint_H.Update(maxCol * hVert, maxX_H, maxRowH * hVert);
                            for (int i = 0; i < col1_theory; i++)
                            {
                                for (int j = 0; j < maxRowH; j++)
                                {
                                    TryAddOffset(offsetsH, angleVert,
                                        new Vector2d(maxCol * hVert + i * wVert + offset_V.X, j * hVert + offset_V.Y));
                                }
                            }
                        }
                        else
                        {
                            // Take actual value, reverse calculate columns
                            int col1Real = 0; 
                            if(wVert > 0) col1Real = (maxX1 - maxCol * hVert) / wVert;
                            
                            int mod1 = 0;
                            if (col1Real * maxRowH > 0) mod1 = num1 % (col1Real * maxRowH);
                            
                            int tempNum1 = num1;
                            for (int i = 0; i < col1Real && tempNum1 > 0; i++)
                            {
                                for (int j = 0; j < maxRowH && tempNum1 > 0; j++)
                                {
                                    tempNum1--;
                                    TryAddOffset(offsetsH, angleVert,
                                        new Vector2d(maxCol * hVert + i * wVert + offset_V.X, j * hVert + offset_V.Y));
                                }
                            }

                            // Update outerContourPoint_H
                            var minX11 = maxCol * hVert;
                            if (mod1 == 0) // Remainder is 0
                            {
                                outerContourPoint_H.Update(minX11, maxX1, maxRowH * hVert);
                            }
                            else
                            {
                                if (col1Real == 1)
                                {
                                    outerContourPoint_H.Update(minX11, maxX1, (maxRowH - mod1) * hVert);
                                }
                                else
                                {
                                    var maxX12 = minX11 + (col1Real - 1) * wVert;
                                    outerContourPoint_H.Update(minX11, maxX12, maxRowH * hVert);
                                    outerContourPoint_H.Update(maxX12, maxX12 + wVert, (maxRowH - mod1) * hVert);
                                }
                            }

                        }
                    }
                    else
                    {
                        // Choose option 2
                        maxX_H = maxX2;
                        if (!merge3) // Non-merge case 3
                        {
                            // Place all horizontally
                            for (int i = 0; i < lastColNum; i++)
                            {
                                TryAddOffset(offsetsH, angleHorz, new Vector2d(maxCol * hVert + offset_H.X, i * wVert + offset_H.Y));
                            }

                            outerContourPoint_H.Update(0, maxX_H, lastColNum * wVert);
                        }
                        else
                        {
                            // First column horizontal
                            for (int i = 0; i < maxRowV; i++)
                            {
                                TryAddOffset(offsetsH, angleHorz, new Vector2d(maxCol * hVert + offset_H.X, i * wVert + offset_H.Y));
                            }

                            outerContourPoint_H.Update(0, (maxCol + 1) * hVert, maxRowV * wVert);
                            // Mixed horizontal/vertical, reverse calculate actual vertical columns
                            num2 -= maxRowV;
                            int col2Real = 0;
                            if(wVert > 0) col2Real = (maxX2 - ((maxCol + 1) * hVert)) / wVert;
                            
                            int mod2 = 0;
                            if (col2Real * maxRowH > 0) mod2 = num2 % (col2Real * maxRowH);
                            
                            for (int i = 0; i < col2Real && num2 > 0; i++)
                            {
                                for (int j = 0; j < maxRowH && num2 > 0; j++)
                                {
                                    num2--;
                                    TryAddOffset(offsetsH, angleVert,
                                        new Vector2d((maxCol + 1) * hVert + i * wVert + offset_V.X, j * hVert + offset_V.Y));
                                }
                            }

                            var minX21 = maxCol * hVert;
                            if (mod2 == 0) // Remainder is 0
                            {
                                outerContourPoint_H.Update(minX21, maxX2, maxRowH * hVert);
                            }
                            else
                            {
                                if (col2Real == 1)
                                {
                                    outerContourPoint_H.Update(minX21, maxX2, (maxRowH - mod2) * hVert);
                                }
                                else
                                {
                                    var maxX22 = minX21 + (col2Real - 1) * wVert;
                                    outerContourPoint_H.Update(minX21, maxX22, maxRowH * hVert);
                                    outerContourPoint_H.Update(maxX22, maxX2, (maxRowH - mod2) * hVert);
                                }
                            }
                        }
                    }
                }
            }

            var num_H = offsetsH.Sum(o => o.Value.Count);
            var solution = new Dictionary<string, Dictionary<double, List<Vector2d>>>
            {
                [template.SourceKey] = offsetsH
            };

            // Horizontal packing result
            var nestingSolution_H = new NestingSolution(solution, maxX_H, num_H, outerContourPoint_H);
            return nestingSolution_H;
        }

        private NestingSolution SinglePartV(
            int maxRowV, int maxRowH, int sheetH, int sheetW, int wVert, int hVert,
            PartItem template, Vector2d offset_H, Vector2d offset_V,
            int totalCount, double angleHorz, double angleVert)
        {
            // 2. Try all vertical
            Dictionary<double, List<Vector2d>> offsetsV = new Dictionary<double, List<Vector2d>>();
            var outerContourPoint_V = new OuterContourPoint(sheetH);
            var maxX_V = 0;

            if (maxRowH == 0) return new NestingSolution(new Dictionary<string, Dictionary<double, List<Vector2d>>>(), 0, 0, outerContourPoint_V);

            // Calculate required columns
            int totalCols_V = Math.Min((totalCount + maxRowH - 1) / maxRowH, sheetW / wVert);

            int countPlaced_V = 0;

            for (int c = 0; c < totalCols_V; c++)
            {
                // Current column X coordinate
                double currentX = c * wVert;

                // Items placed in current column (for contour update)
                int itemsInCol = 0;

                // Fill this column
                for (int r = 0; r < maxRowH; r++)
                {
                    if (countPlaced_V >= totalCount) break;

                    // Calculate coords: X = col * width, Y = row * height
                    // Use angleVert (0 deg) and offset_V
                    TryAddOffset(offsetsV, angleVert,
                        new Vector2d(currentX + offset_V.X, r * hVert + offset_V.Y));

                    countPlaced_V++;
                    itemsInCol++;
                }

                // Update contour and MaxX
                // In vertical mode, each column width is fixed to wVert
                int currentMaxX = (c + 1) * wVert;
                maxX_V = currentMaxX;
                outerContourPoint_V.Update(currentX, currentMaxX, itemsInCol * hVert);
            }

            // Build vertical packing result
            var num_V = offsetsV.Sum(o => o.Value.Count);
            if (totalCount > num_V)
            {
                // Remaining items exist, try horizontal packing at the top
                int newH = sheetH - maxRowH * hVert;
                if (newH > wVert)
                {
                    int hStart = maxRowH * hVert; // Current max Y
                    int maxRowH_new = newH / wVert; // New row count
                    if (maxRowH_new > 0)
                    {
                        int reaminNum = totalCount - num_V;
                        int colMax = Math.Min((reaminNum + maxRowH_new - 1) / maxRowH_new, sheetW / hVert);
                        int mod = reaminNum % (colMax * maxRowH_new); // Whether it exactly fills a whole row
                        for (int i = 0; i < colMax && reaminNum > 0; i++)
                        {
                            for (int j = 0; j < maxRowH_new && reaminNum > 0; j++)
                            {
                                num_V++;
                                reaminNum--;
                                TryAddOffset(offsetsV, angleHorz,
                                    new Vector2d(i * hVert + offset_H.X, hStart + j * wVert + offset_H.Y));
                            }
                        }

                        maxX_V = Math.Max(maxX_V, colMax * hVert);
                        if (mod == 0) // Indicates no gaps in rows
                        {
                            outerContourPoint_V.Update(0, colMax * hVert, hStart + maxRowH_new * wVert);
                        }
                        else
                        {
                            if (colMax == 1)
                            {
                                // Special case, only one row with gaps
                                outerContourPoint_V.Update(0, colMax * hVert, hStart + (maxRowH_new - mod) * wVert);
                            }
                            else
                            {
                                outerContourPoint_V.Update(0, (colMax - 1) * hVert, hStart + maxRowH_new * wVert);
                                outerContourPoint_V.Update((colMax - 1) * hVert, colMax * hVert, hStart + maxRowH_new * wVert);
                            }
                        }
                    }
                }
            }
            
            var solutionV = new Dictionary<string, Dictionary<double, List<Vector2d>>>
            {
                [template.SourceKey] = offsetsV
            };
            var nestingSolution_V = new NestingSolution(solutionV, maxX_V, num_V, outerContourPoint_V);
            return nestingSolution_V;
        }

        private NestingSolution SinglePartMixHV(
            int maxRowV, int maxRowH, int sheetH, int sheetW, int wVert, int hVert,
            PartItem template, Vector2d offset_H, Vector2d offset_V,
            int totalCount, double angleHorz, double angleVert, bool optimize = false)
        {
            // Iterate based on max vertical rows, find combination with min remaining height
            int group_V = 0; // Vertical
            int group_H = 0; // Horizontal
            var minHeight = int.MaxValue;
            
            if (wVert == 0) return new NestingSolution();

            for (int i = 0; i < maxRowH; i++)
            {
                var v_MaxRowH = (sheetH - (i + 1) * hVert) / wVert; // Max horizontal value
                var curHeight = sheetH - (i + 1) * hVert - v_MaxRowH * wVert; // Remaining height value
                if (curHeight < minHeight)
                {
                    group_V = i + 1;
                    group_H = v_MaxRowH;
                    minHeight = curHeight;
                }
            }

            if (group_V == 0 || group_H == 0)
            {
                return new NestingSolution();
            }

            // Pack the best combination
            // 1. Maintain a stepped list for top/bottom, value is next placeable bottom-left (x,y), third value is y-length, list count max 2, min 1
            int interval_V_Leng = group_V * hVert; // Vertical Y interval
            int interval_H_Leng = group_H * wVert; // Horizontal Y interval
            List<(int, int, int)> interval_V = new List<(int, int, int)> { (0, 0, interval_V_Leng) }; // Vertical
            List<(int, int, int)> interval_H = new List<(int, int, int)> { (0, interval_V_Leng, interval_H_Leng) }; // Horizontal

            // 2. Start packing
            int remainNum = totalCount;
            Dictionary<double, List<Vector2d>> offsets = new Dictionary<double, List<Vector2d>>();
            int curMaxX_V = 0; // Max X for vertical
            int curMaxX_H = 0; // Max X for horizontal

            // 3. First pack the first vertical column
            for (int i = 0; i < group_V && remainNum > 0; i++)
            {
                int i_hVert = i * hVert;
                TryAddOffset(offsets, angleVert, new Vector2d(offset_V.X, i_hVert + offset_V.Y));
                remainNum--;
                if (i < group_V - 1)
                {
                    interval_V = new List<(int, int, int)>
                    {
                        (wVert, 0, i_hVert),
                        (0, i_hVert, interval_V_Leng - i_hVert)
                    };
                }
                else
                {
                    interval_V = new List<(int, int, int)>
                    {
                        (wVert, 0, interval_V_Leng),
                    };
                }
            }

            curMaxX_V = wVert;
            bool fillable_H = false; // Mark horizontal as fillable later
            bool fillable_V = false;
            // 4. Iterate remaining items
            while (remainNum > 0)
            {
                // Check horizontal direction first, ensure it doesn't exceed vertical maxX
                if ((curMaxX_H + hVert <= curMaxX_V || fillable_H) && !fillable_V)
                {
                    if (interval_H.Count == 1)
                    {
                        // If it's a new column
                        int curMaxH = interval_H[0].Item1 + hVert;
                        if (curMaxH > sheetW) break;
                        curMaxX_H = curMaxH; // Update max value
                        remainNum--; // Quantity - 1
                        TryAddOffset(offsets, angleHorz,
                            new Vector2d(interval_H[0].Item1 + offset_H.X, interval_H[0].Item2 + offset_H.Y));
                        var newInterval = (interval_H[0].Item1 + hVert, interval_H[0].Item2, wVert); // Update bottom-most interval
                        if (wVert == interval_H_Leng)
                        {
                            // If only one row
                            interval_H = new List<(int, int, int)> { newInterval };
                        }
                        else
                        {
                            // Multiple rows, fillable later
                            var y2 = interval_H[0].Item2 + wVert; // Calculate current filled height
                            fillable_H = true;
                            interval_H = new List<(int, int, int)>
                            {
                                newInterval,
                                (interval_H[0].Item1, y2, interval_H_Leng - (interval_H[0].Item3 + wVert)),
                            };
                        }
                    }
                    else
                    {
                        // If fillable, pack into interval_H[1] until full or no items left
                        while (remainNum > 0)
                        {
                            var y3 = interval_H[0].Item3 + wVert; // Check if directly filled
                            if (y3 == interval_H_Leng)
                            {
                                // Directly fill the gap
                                TryAddOffset(offsets, angleHorz,
                                    new Vector2d(interval_H[1].Item1 + offset_H.X, interval_H[1].Item2 + offset_H.Y));
                                interval_H = new List<(int, int, int)> { (interval_H[0].Item1, interval_H[0].Item2, y3) };
                                remainNum--;
                                fillable_H = false;
                                break;
                            }

                            // Still not filled, update interval values
                            TryAddOffset(offsets, angleHorz,
                                new Vector2d(interval_H[1].Item1 + offset_H.X, interval_H[1].Item2 + offset_H.Y));
                            interval_H = new List<(int, int, int)>
                            {
                                (interval_H[0].Item1, interval_H[0].Item2, y3),
                                (interval_H[1].Item1, interval_H[1].Item2 + wVert, interval_H_Leng - y3),
                            };
                            remainNum--;
                        }
                    }
                }
                else
                {
                    // Place vertically
                    if (interval_V.Count == 1)
                    {
                        // Interval count 1 means need new column
                        int curMaxV = interval_V[0].Item1 + wVert;
                        if (curMaxV > sheetW) break;
                        curMaxX_V = curMaxV;
                        remainNum--; // Quantity - 1
                        TryAddOffset(offsets, angleVert,
                            new Vector2d(interval_V[0].Item1 + offset_V.X, interval_V[0].Item2 + offset_V.Y));
                        var newInterval = (interval_V[0].Item1 + wVert, interval_V[0].Item2, hVert); // Update bottom-most interval
                        if (hVert == interval_V_Leng)
                        {
                            // Can only place one row
                            fillable_V = false;
                            interval_V = new List<(int, int, int)> { newInterval };
                        }
                        else
                        {
                            fillable_V = true;
                            interval_V = new List<(int, int, int)>
                            {
                                newInterval,
                                (interval_V[0].Item1, interval_V[0].Item2 + hVert, interval_V_Leng - newInterval.Item3),
                            };
                        }
                    }
                    else
                    {
                        // Filling case
                        while (remainNum > 0)
                        {
                            TryAddOffset(offsets, angleVert,
                                new Vector2d(interval_V[1].Item1 + offset_V.X, interval_V[1].Item2 + offset_V.Y));
                            var y4 = interval_V[0].Item3 + hVert; // Check if directly filled
                            if (y4 == interval_V_Leng)
                            {
                                // Directly fill the gap
                                fillable_V = false;
                                interval_V = new List<(int, int, int)> { (interval_V[0].Item1, interval_V[0].Item2, y4) };
                                remainNum--;
                                break;
                            }

                            // Still not filled, update interval values
                            fillable_V = true;
                            interval_V = new List<(int, int, int)>
                            {
                                (interval_V[0].Item1, interval_V[0].Item2, y4),
                                (interval_V[1].Item1, interval_V[1].Item2 + hVert, interval_V_Leng - y4),
                            };
                            remainNum--;
                        }
                    }
                }
            }

            // 4-1 Still have remaining parts 
            var numCur = offsets.Sum(o => o.Value.Count);
            if (totalCount - numCur > 0)
            {
                // No subsequent fine-tuning, prioritize filling gaps
                optimize = false;
                int numRemain = totalCount - numCur;
                // TODO: In this case, both (interval_V, interval_H) must have no gaps; identical maxX means cannot continue placement;
                if (interval_V.Count == 1 && interval_H.Count == 1)
                {
                    if (curMaxX_H != curMaxX_V)
                    {
                        // Must be curMaxX_H < curMaxX_V, and can only place remaining in horizontal area
                        int newHeight = sheetH - interval_V_Leng;
                        int newWidth = sheetW - interval_H[0].Item1;

                        // Three cases: 1. All vertical; 2. All horizontal; 3. Mixed (can be combined with 2)
                        // 1. All vertical
                        int num1 = 0;
                        int max1 = interval_H[0].Item1;
                        if (newHeight >= hVert && newWidth >= wVert) // Can place at least one vertically
                        {
                            int num1RowMax = newHeight / hVert; // Theoretical max rows
                            int num1ColMax = Math.Min((numRemain + num1RowMax - 1) / num1RowMax, newWidth / wVert); // Theoretical max columns
                            if (numRemain < num1ColMax * num1RowMax)
                            {
                                // Place all
                                int numRemain1 = numRemain;
                                for (int i = 0; i < num1ColMax && numRemain1 > 0; i++)
                                {
                                    for (int j = 0; j < num1RowMax && numRemain1 > 0; j++)
                                    {
                                        numRemain1--;
                                    }

                                    max1 += wVert;
                                }

                                num1 = numRemain; // Remaining value
                            }
                            else
                            {
                                // Place partial
                                num1 = num1ColMax * num1RowMax; // Theoretical limit value
                                max1 += num1ColMax * wVert;
                            }
                        }

                        // 2. Mixed horizontal/vertical
                        int num2 = 0;
                        int max2 = interval_H[0].Item1;
                        bool merge2 = false;
                        if (newHeight >= wVert && newWidth >= hVert) // Can place at least one horizontally
                        {
                            int num2RowMax = newHeight / wVert; // Theoretical max rows
                            int num2ColMax = Math.Min((numRemain + num2RowMax - 1) / num2RowMax, newWidth / hVert); // Theoretical max columns
                            if (numRemain < num2RowMax * num2ColMax)
                            {
                                // Place all
                                int numRemain2 = numRemain;
                                for (int i = 0; i < num2ColMax && numRemain2 > 0; i++)
                                {
                                    for (int j = 0; j < num2RowMax && numRemain2 > 0; j++)
                                    {
                                        numRemain2--;
                                    }

                                    max2 += hVert;
                                }

                                num2 = numRemain; // Remaining value
                            }
                            else
                            {
                                // Place partial
                                num2 = num2ColMax * num2RowMax; // Theoretical limit value
                                max2 = interval_H[0].Item1 + num2ColMax * hVert;
                                if (sheetW - max2 > wVert && sheetH - interval_V_Leng > hVert)
                                {
                                    // Can place remaining at the end
                                    merge2 = true;
                                    int remainNum3 = numRemain - num2;
                                    int width3 = sheetW - max2;
                                    int height3 = sheetH - interval_V_Leng;
                                    int row3 = height3 / hVert; // Theoretical max rows
                                    int col3 = Math.Min((remainNum3 + row3 - 1) / row3, width3 / hVert); // Theoretical max columns
                                    if (remainNum3 < col3 * row3)
                                    {
                                        // Place all
                                        int numRemain4 = remainNum3;
                                        for (int i = 0; i < num2ColMax && numRemain4 > 0; i++)
                                        {
                                            for (int j = 0; j < num2RowMax && numRemain4 > 0; j++)
                                            {
                                                numRemain4--;
                                            }

                                            max2 += wVert;
                                        }

                                        num2 += remainNum3;
                                    }
                                    else
                                    {
                                        // Place partial
                                        num2 += col3 * row3;
                                        max2 += col3 * wVert;
                                    }
                                }
                            }
                        }

                        if (num1 > 0 || num2 > 0)
                        {
                            if (num1 > 0 && (num1 > num2 || (num1 == num2 && max1 < max2)))
                            {
                                // Choose option 1, reverse calculate columns
                                int col1Real = 0;
                                if (wVert > 0) col1Real = (max1 - interval_H[0].Item1) / wVert;
                                
                                int row1Real = 0;
                                if (hVert > 0) row1Real = (sheetH - interval_V_Leng) / hVert;
                                
                                int mod = 0; 
                                if (col1Real * row1Real > 0) mod = num1 % (col1Real * row1Real); // Check for gaps
                                
                                int num1Copy = num1;
                                for (int i = 0; i < col1Real && num1Copy > 0; i++)
                                {
                                    for (int j = 0; j < row1Real && num1Copy > 0; j++)
                                    {
                                        num1Copy--;
                                        TryAddOffset(offsets, angleVert,
                                            new Vector2d(interval_H[0].Item1 + i * wVert + offset_V.X,
                                                interval_H[0].Item2 + j * hVert + offset_V.Y));
                                    }
                                }

                                if (mod == 0)
                                {
                                    interval_H = new List<(int, int, int)>
                                        { (interval_H[0].Item1 + col1Real * wVert, interval_H[0].Item2, sheetH - interval_V_Leng) };
                                }
                                else
                                {
                                    interval_H = new List<(int, int, int)>
                                    {
                                        (interval_H[0].Item1 + col1Real * wVert, interval_H[0].Item2, (row1Real - mod) * hVert),
                                        (interval_H[0].Item1, interval_H[0].Item2 + (row1Real - mod) * hVert, mod * hVert),
                                    };
                                }
                            }
                            else if (num2 > 0 && (num2 > num1 || (num2 == num1 && max2 < max1)))
                            {
                                if (!merge2) // No vertical, only horizontal
                                {
                                    // Reverse calculate columns
                                    int col2Real = 0;
                                    if(hVert > 0) col2Real = (max2 - interval_H[0].Item1) / hVert;
                                    
                                    int row2Real = 0; 
                                    if (wVert > 0) row2Real = (sheetH - interval_V_Leng) / wVert;
                                    
                                    int mod = 0; 
                                    if (col2Real * row2Real > 0) mod = num2 % (col2Real * row2Real); // Check for gaps
                                    
                                    int num2Copy = num2;
                                    for (int i = 0; i < col2Real && num2Copy > 0; i++)
                                    {
                                        for (int j = 0; j < row2Real && num2Copy > 0; j++)
                                        {
                                            num2Copy--;
                                            TryAddOffset(offsets, angleHorz,
                                                new Vector2d(interval_H[0].Item1 + i * hVert + offset_H.X,
                                                    interval_H[0].Item2 + j * wVert + offset_H.Y));
                                        }
                                    }

                                    if (mod == 0)
                                    {
                                        interval_H = new List<(int, int, int)>
                                            { (interval_H[0].Item1 + col2Real * hVert, interval_H[0].Item2, sheetH - interval_V_Leng) };
                                    }
                                    else
                                    {
                                        interval_H = new List<(int, int, int)>
                                        {
                                            (interval_H[0].Item1 + col2Real * hVert, interval_H[0].Item2, (row2Real - mod) * wVert),
                                            (interval_H[0].Item1, interval_H[0].Item2 + (row2Real - mod) * wVert, mod * wVert),
                                        };
                                    }
                                }
                                else // Mixed horizontal/vertical
                                {
                                    // Place horizontal first
                                    int num3 = num2;
                                    int num2RowMaxH = newHeight / wVert; // Theoretical max rows
                                    int num2ColMaxH = Math.Min((numRemain + num2RowMaxH - 1) / num2RowMaxH, newWidth / hVert); // Theoretical max columns
                                    for (int i = 0; i < num2RowMaxH; i++)
                                    {
                                        for (int j = 0; j < num2ColMaxH; j++)
                                        {
                                            num3--;
                                            TryAddOffset(offsets, angleHorz,
                                                new Vector2d(interval_H[0].Item1 + i * hVert + offset_H.X,
                                                    interval_H[0].Item2 + j * wVert + offset_H.Y));
                                        }
                                    }

                                    interval_H = new List<(int, int, int)>
                                        { (interval_H[0].Item1 + num2ColMaxH * hVert, interval_H[0].Item2, sheetH - interval_V_Leng) };
                                    if (num3 > 0)
                                    {
                                        // Then place vertical
                                        int col3Real = 0; 
                                        if (wVert > 0) col3Real = (max2 - num2ColMaxH * hVert - interval_H[0].Item1) / wVert; // Reverse calculate columns
                                        
                                        int row3Real = 0; 
                                        if (hVert > 0) row3Real = (sheetH - interval_V_Leng) / hVert;
                                        
                                        int num3Copy = num3;
                                        int mod = 0;
                                        if (col3Real * row3Real > 0) mod = num3 % (col3Real * row3Real); // Check for gaps
                                        
                                        for (int i = 0; i < col3Real && num3Copy > 0; i++)
                                        {
                                            for (int j = 0; j < row3Real && num3Copy > 0; j++)
                                            {
                                                num3Copy--;
                                                TryAddOffset(offsets, angleHorz,
                                                    new Vector2d(interval_H[0].Item1 + i * wVert + offset_H.X,
                                                        interval_H[0].Item2 + j * hVert + offset_H.Y));
                                            }
                                        }

                                        if (mod == 0)
                                        {
                                            interval_H = new List<(int, int, int)>
                                                { (interval_H[0].Item1 + col3Real * hVert, interval_H[0].Item2, sheetH - interval_V_Leng) };
                                        }
                                        else
                                        {
                                            interval_H = new List<(int, int, int)>
                                            {
                                                (interval_H[0].Item1 + col3Real * wVert, interval_H[0].Item2, (row3Real - mod) * hVert),
                                                (interval_H[0].Item1, interval_H[0].Item2 + (row3Real - mod) * hVert, mod * hVert),
                                            };
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                else
                {
                    Console.WriteLine("Rect packing error! Please check!!!!!!");
                }
            }

            // 5. Fine-tune result: top horizontal area might fit vertical ones, and after adjustment, maxX is smaller
            if (optimize && interval_V[0].Item1 > 0 && interval_H[0].Item1 > 0)
            {
                // 5-1 Horizontal area can fit at least one vertical
                var reminTopY = sheetH - interval_V_Leng - interval_H_Leng; // # Calculate small remaining gap at top
                if (sheetH - interval_V_Leng >= hVert)
                {
                    // 5-1-1 If only one vertical column
                    if (group_V == 1)
                    {
                        if (interval_H.Count == 1) // Only handle case where horizontal is complete with no gaps
                        {
                            interval_H = new List<(int, int, int)>
                                { (interval_H[0].Item1, interval_H[0].Item2, sheetH - interval_V_Leng) }; // Fill the last small top area
                            bool is_has_remainY = false;
                            var newVectors_V = new List<Vector2d>();
                            while (true) // Loop attempt
                            {
                                if (interval_H.Count == 1)
                                {
                                    if (curMaxX_H + wVert < curMaxX_V - thresholdV)
                                    {
                                        // Adjusted maxX is smaller
                                        var last = offsets[angleVert].Last();
                                        offsets[angleVert].Remove(last);
                                        curMaxX_H += wVert;
                                        curMaxX_V -= wVert;
                                        newVectors_V.Add(new Vector2d(interval_H[0].Item1 + offset_V.X,
                                            interval_H[0].Item2 + offset_V.Y));
                                        if (hVert == sheetH - interval_V_Leng)
                                        {
                                            interval_H = new List<(int, int, int)>
                                                { (interval_H[0].Item1 + wVert, interval_H[0].Item2, interval_H[0].Item3) };
                                        }
                                        else
                                        {
                                            // After filling, check if gap can fit vertical, otherwise flatten
                                            if (hVert > sheetH - interval_V_Leng - hVert)
                                            {
                                                interval_H = new List<(int, int, int)>
                                                    { (interval_H[0].Item1 + wVert, interval_H[0].Item2, interval_H[0].Item3) };
                                            }
                                            else
                                            {
                                                // Can continue filling vertical
                                                is_has_remainY = true; // If not, exit loop
                                                interval_H = new List<(int, int, int)>
                                                {
                                                    (interval_H[0].Item1 + wVert, interval_H[0].Item2, hVert),
                                                    (interval_H[0].Item1, interval_H[0].Item2 + hVert,
                                                        interval_H_Leng - hVert + reminTopY),
                                                };
                                            }
                                        }
                                    }
                                    else
                                    {
                                        break;
                                    }
                                }
                                else
                                {
                                    if (is_has_remainY && curMaxX_V - wVert > curMaxX_H)
                                    {
                                        // Filling remaining gaps
                                        var last = offsets[angleVert].Last();
                                        offsets[angleVert].Remove(last);
                                        curMaxX_V -= wVert;
                                        newVectors_V.Add(new Vector2d(interval_H[1].Item1 + offset_V.X,
                                            interval_H[1].Item2 + offset_V.Y));
                                        if (interval_H[1].Item3 == hVert ||
                                            interval_H_Leng - interval_H[0].Item3 - hVert < hVert) // Whether completely filled or cannot fill further
                                        {
                                            interval_H = new List<(int, int, int)>
                                                { (interval_H[0].Item1, interval_H[0].Item2, sheetH - interval_V_Leng) };
                                        }
                                        else
                                        {
                                            // Can continue filling
                                            interval_H = new List<(int, int, int)>
                                            {
                                                (interval_H[0].Item1, interval_H[0].Item2, interval_H[0].Item3 + hVert),
                                                (interval_H[1].Item1, interval_H[1].Item2 + hVert,
                                                    sheetH - interval_H[0].Item3 - hVert),
                                            };
                                        }
                                    }
                                    else
                                    {
                                        break;
                                    }
                                }
                            }

                            foreach (var offset in newVectors_V)
                            {
                                TryAddOffset(offsets, angleVert, offset);
                            }
                        }
                        else
                        {
                            // Calculate top area
                            var remainYH = sheetH - interval_V_Leng - interval_H[0].Item3;
                            var numH = remainYH / wVert; // Calculate limit value that can fit
                            if (numH > 0 && curMaxX_H < curMaxX_V - thresholdV)
                            {
                                var newVectorsH = new List<Vector2d>();
                                var lastColNum = 0; 
                                if(hVert > 0) lastColNum = interval_V[0].Item3 / hVert;
                                
                                if (numH >= lastColNum)
                                {
                                    // Move all
                                    // Place all
                                    for (int i = 0; i < lastColNum; i++)
                                    {
                                        newVectorsH.Add(new Vector2d(interval_H[1].Item1 + offset_H.X,
                                            interval_H[1].Item2 + i * wVert + offset_H.Y));
                                    }

                                    int lastVColNum = offsets[angleVert].Count;
                                    offsets[angleVert] = offsets[angleVert].Take(lastVColNum - lastColNum).ToList();
                                    curMaxX_V -= wVert;
                                    interval_V = new List<(int, int, int)>
                                    {
                                        (interval_V[0].Item1 - wVert, interval_V[0].Item2, interval_V_Leng),
                                    };

                                    interval_H = new List<(int, int, int)>
                                    {
                                        (interval_H[0].Item1, interval_H[0].Item2,
                                            interval_H[0].Item3 + lastColNum * wVert),
                                        (interval_H[1].Item1, interval_H[1].Item2 + lastColNum * hVert,
                                            sheetH - interval_V_Leng - interval_H[0].Item3 - lastColNum * wVert)
                                    }; // No gaps left
                                }
                                else
                                {
                                    // Move partial
                                    // Can only place partial
                                    for (int i = 0; i < numH; i++)
                                    {
                                        newVectorsH.Add(new Vector2d(interval_H[1].Item1 + offset_H.X,
                                            interval_H[1].Item2 + i * wVert + offset_H.Y));
                                    }

                                    int lastVColNum = offsets[angleVert].Count;
                                    offsets[angleVert] = offsets[angleVert].Take(lastVColNum - numH).ToList();

                                    if (interval_V.Count == 1)
                                    {
                                        // Dig a hole
                                        interval_V = new List<(int, int, int)>
                                        {
                                            (interval_V[0].Item1, interval_V[0].Item2, interval_V_Leng - numH * hVert),
                                            (interval_V[0].Item1 - wVert,
                                                interval_V[0].Item2 + interval_V_Leng - numH * hVert, numH * hVert),
                                        };
                                    }
                                    else
                                    {
                                        interval_V = new List<(int, int, int)>
                                        {
                                            (interval_V[0].Item1, interval_V[0].Item2,
                                                interval_V[0].Item3 - numH * hVert),
                                            (interval_V[1].Item1, interval_V[1].Item2 - numH * hVert,
                                                interval_V[1].Item3 + numH * hVert),
                                        };
                                    }

                                    interval_H = new List<(int, int, int)>
                                        { (interval_H[1].Item1, interval_H[0].Item2, sheetH - interval_V_Leng) };
                                }

                                offsets[angleHorz].AddRange(newVectorsH);
                            }
                        }
                    }
                    else
                    {
                        // In case of multiple vertical columns, only adjust the last column each time
                        int remainHY = sheetH - interval_V_Leng - interval_H[0].Item3;
                        if (interval_H.Count == 1 || remainHY < hVert)
                        {
                            // Check if horizontal placement possible, and maxX reduction exceeds threshold
                            var newVectorH = new List<Vector2d>();
                            while (interval_H.Count > 1 && remainHY > wVert && curMaxX_V - curMaxX_H > thresholdV)
                            {
                                int rowHMax = remainHY / wVert; // Max horizontal rows
                                if (interval_V.Count == 1) // Last vertical column is full
                                {
                                    if (maxRowV <= rowHMax) // Place all in horizontal area
                                    {
                                        for (int i = 0; i < maxRowV; i++)
                                        {
                                            var last = offsets[angleVert].Last();
                                            offsets[angleVert].Remove(last);
                                            newVectorH.Add(new Vector2d(interval_H[1].Item1 + offset_H.X,
                                                interval_H[1].Item2 + i * wVert + offset_H.Y));
                                        }

                                        curMaxX_V -= wVert;
                                        remainHY -= maxRowV * wVert;
                                        interval_V = new List<(int, int, int)>
                                            { (interval_V[0].Item1 - wVert, interval_V[0].Item2, interval_V_Leng) };
                                        if (maxRowV * wVert == remainHY)
                                        {
                                            interval_H = new List<(int, int, int)>
                                            {
                                                (interval_H[0].Item1, interval_H[0].Item2, sheetH - interval_V_Leng)
                                            };
                                        }
                                        else
                                        {
                                            int yrow = maxRowV * wVert;
                                            interval_H = new List<(int, int, int)>
                                            {
                                                (interval_H[0].Item1, interval_H[0].Item2, interval_H[0].Item3 + yrow),
                                                (interval_H[1].Item1, interval_H[1].Item2 + yrow, remainHY - yrow),
                                            };
                                        }
                                    }
                                    else
                                    {
                                        // Place partial in horizontal area
                                        for (int i = 0; i < rowHMax; i++)
                                        {
                                            var last = offsets[angleVert].Last();
                                            offsets[angleVert].Remove(last);
                                            newVectorH.Add(new Vector2d(interval_H[1].Item1 + offset_H.X,
                                                interval_H[1].Item2 + i * wVert + offset_H.Y));
                                        }

                                        remainHY -= rowHMax * wVert;
                                        interval_H = new List<(int, int, int)>
                                            { (interval_H[0].Item1, interval_H[0].Item2, sheetH - interval_V_Leng) };
                                        interval_V = new List<(int, int, int)>
                                        {
                                            (interval_V[0].Item1, interval_V[0].Item2,
                                                interval_V_Leng - rowHMax * hVert),
                                            (interval_V[0].Item1 - wVert, interval_V[0].Item2 + rowHMax * hVert,
                                                rowHMax * hVert),
                                        };
                                    }
                                }
                                else
                                {
                                    // Calculate quantity in last column
                                    int lastVCol = 0;
                                    if(hVert > 0) lastVCol = interval_V[0].Item3 / hVert;
                                    
                                    if (lastVCol <= rowHMax)
                                    {
                                        // Place all in horizontal area
                                        for (int i = 0; i < lastVCol; i++)
                                        {
                                            var last = offsets[angleVert].Last();
                                            offsets[angleVert].Remove(last);
                                            newVectorH.Add(new Vector2d(interval_H[1].Item1 + offset_H.X,
                                                interval_H[1].Item2 + i * wVert + offset_H.Y));
                                        }

                                        curMaxX_V -= wVert;
                                        remainHY -= lastVCol * wVert;
                                        interval_V = new List<(int, int, int)>
                                            { (interval_V[0].Item1 - wVert, interval_V[0].Item2, interval_V_Leng) };
                                        if (lastVCol * wVert == remainHY)
                                        {
                                            interval_H = new List<(int, int, int)>
                                            {
                                                (interval_H[0].Item1, interval_H[0].Item2, sheetH - interval_V_Leng)
                                            };
                                        }
                                        else
                                        {
                                            int yrow = lastVCol * wVert;
                                            interval_H = new List<(int, int, int)>
                                            {
                                                (interval_H[0].Item1, interval_H[0].Item2, interval_H[0].Item3 + yrow),
                                                (interval_H[1].Item1, interval_H[1].Item2 + yrow, remainHY - yrow),
                                            };
                                        }
                                    }
                                    else
                                    {
                                        // Place partial in horizontal area
                                        for (int i = 0; i < rowHMax; i++)
                                        {
                                            var last = offsets[angleVert].Last();
                                            offsets[angleVert].Remove(last);
                                            newVectorH.Add(new Vector2d(interval_H[1].Item1 + offset_H.X,
                                                interval_H[1].Item2 + i * wVert + offset_H.Y));
                                        }

                                        remainHY -= rowHMax * wVert;
                                        interval_H = new List<(int, int, int)>
                                            { (interval_H[0].Item1, interval_H[0].Item2, sheetH - interval_V_Leng) };
                                        interval_V = new List<(int, int, int)>
                                        {
                                            (interval_V[0].Item1, interval_V[0].Item2,
                                                interval_V_Leng - rowHMax * hVert),
                                            (interval_V[0].Item1 - wVert, interval_V[0].Item2 + rowHMax * hVert,
                                                rowHMax * hVert),
                                        };
                                    }
                                }
                            }

                            if (newVectorH.Count > 0)
                            {
                                offsets[angleHorz].AddRange(newVectorH);
                            }

                            // Try vertical
                            interval_H = new List<(int, int, int)>
                            {
                                (interval_H[0].Item1, interval_H[0].Item2, sheetH - interval_V_Leng)
                            }; // Fill the last small top area
                            optCols(ref interval_H, sheetH, interval_V_Leng, ref curMaxX_H, wVert, ref curMaxX_V,
                                offsets, angleVert, angleHorz, offset_H, offset_V, ref interval_V, hVert,
                                interval_H_Leng);
                        }
                        else
                        {
                            // Check if the last horizontal column can be vertical itself
                            int lastNum = 0;
                            if(wVert > 0) lastNum = interval_H[0].Item3 / wVert;
                            
                            if (lastNum * hVert <= sheetH - interval_V_Leng)
                            {
                                var newVectorV = new List<Vector2d>();
                                var yStart = interval_H[0].Item2;
                                var oldOffsetxH = offsets[angleHorz];
                                for (int i = 0; i < lastNum; i++)
                                {
                                    newVectorV.Add(new Vector2d(interval_H[1].Item1, yStart + i * hVert));
                                }

                                offsets[angleHorz] = oldOffsetxH.Take(oldOffsetxH.Count - lastNum).ToList();
                                curMaxX_H = interval_H[1].Item1 + wVert;
                                // Check if remaining can continue adjusting vertical
                                if (sheetH - interval_V_Leng - lastNum * hVert < hVert)
                                {
                                    interval_H = new List<(int, int, int)>
                                        { (interval_H[1].Item1 + wVert, yStart, sheetH - interval_V_Leng) };
                                }
                                else
                                {
                                    interval_H = new List<(int, int, int)>
                                    {
                                        (interval_H[1].Item1 + wVert, yStart, lastNum * hVert),
                                        (interval_H[1].Item1, yStart + lastNum * hVert,
                                            sheetH - interval_V_Leng - lastNum * hVert),
                                    };
                                }

                                if (interval_H.Count() == 1)
                                {
                                    if (interval_H[0].Item1 + wVert < curMaxX_V) // Check if worth continuing, maxX smaller
                                    {
                                        optCols(ref interval_H, sheetH, interval_V_Leng, ref curMaxX_H, wVert,
                                            ref curMaxX_V,
                                            offsets, angleVert, angleHorz, offset_H, offset_V, ref interval_V, hVert,
                                            interval_H_Leng);
                                    }
                                }
                                else
                                {
                                    var reaminY1 = 0;
                                    if(hVert > 0) reaminY1 = interval_H[1].Item3 / hVert; // How many more vertical ones can fit
                                    
                                    if (interval_V.Count() == 1 && reaminY1 > 0) // Last vertical column is full
                                    {
                                        if (reaminY1 >= group_V) // Place all
                                        {
                                            for (int i = 0; i < group_V; i++)
                                            {
                                                newVectorV.Add(new Vector2d(interval_H[1].Item1,
                                                    interval_H[1].Item2 + i * hVert));
                                            }

                                            curMaxX_V -= wVert;
                                            int lastVColNum = offsets[angleVert].Count;
                                            offsets[angleVert] =
                                                offsets[angleVert].Take(lastVColNum - group_V).ToList();
                                            interval_V = new List<(int, int, int)>
                                            {
                                                (interval_V[0].Item1 - wVert, interval_V[0].Item2, interval_V_Leng)
                                            };
                                            interval_H = new List<(int, int, int)>
                                            {
                                                (interval_H[1].Item1, interval_H[0].Item2, sheetH - interval_V_Leng)
                                            };
                                        }
                                        else
                                        {
                                            // Can only place partial
                                            for (int i = 0; i < reaminY1; i++)
                                            {
                                                newVectorV.Add(new Vector2d(interval_H[1].Item1,
                                                    interval_H[1].Item2 + i * hVert));
                                            }

                                            int lastVColNum = offsets[angleVert].Count;
                                            offsets[angleVert] =
                                                offsets[angleVert].Take(lastVColNum - reaminY1).ToList();
                                            interval_V = new List<(int, int, int)>
                                            {
                                                (interval_V[0].Item1, interval_V[0].Item2,
                                                    interval_V_Leng - reaminY1 * hVert),
                                                (interval_V[0].Item1 - wVert,
                                                    interval_V[0].Item2 + interval_V_Leng - reaminY1 * hVert,
                                                    reaminY1 * hVert),
                                            };
                                            interval_H = new List<(int, int, int)>
                                            {
                                                (interval_H[1].Item1, interval_H[0].Item2, sheetH - interval_V_Leng)
                                            };
                                        }
                                    }
                                    else
                                    {
                                        if (reaminY1 > 0)
                                        {
                                            // Last vertical column is not full
                                            var lastCol = 0; 
                                            if(hVert > 0) lastCol = interval_V[0].Item3 / hVert;
                                            
                                            if (reaminY1 >= lastCol) // Place all
                                            {
                                                for (int i = 0; i < lastCol; i++)
                                                {
                                                    newVectorV.Add(new Vector2d(interval_H[1].Item1,
                                                        interval_H[1].Item2 + i * hVert));
                                                }

                                                curMaxX_V -= wVert;
                                                int lastVColNum = offsets[angleVert].Count;
                                                offsets[angleVert] =
                                                    offsets[angleVert].Take(lastVColNum - lastCol).ToList();
                                                interval_V = new List<(int, int, int)>
                                                {
                                                    (interval_V[0].Item1 - wVert, interval_V[0].Item2, interval_V_Leng)
                                                };
                                                interval_H = new List<(int, int, int)>
                                                {
                                                    (interval_H[1].Item1, interval_H[0].Item2, sheetH - interval_V_Leng)
                                                };
                                            }
                                            else
                                            {
                                                // Can only place partial
                                                for (int i = 0; i < reaminY1; i++)
                                                {
                                                    newVectorV.Add(new Vector2d(interval_H[1].Item1,
                                                        interval_H[1].Item2 + i * hVert));
                                                }

                                                int lastVColNum = offsets[angleVert].Count;
                                                offsets[angleVert] =
                                                    offsets[angleVert].Take(lastVColNum - reaminY1).ToList();
                                                interval_V = new List<(int, int, int)>
                                                {
                                                    (interval_V[0].Item1, interval_V[0].Item2,
                                                        interval_V_Leng - reaminY1 * hVert),
                                                    (interval_V[0].Item1 - wVert,
                                                        interval_V[0].Item2 + interval_V_Leng - reaminY1 * hVert,
                                                        reaminY1 * hVert),
                                                };
                                                interval_H = new List<(int, int, int)>
                                                {
                                                    (interval_H[1].Item1, interval_H[0].Item2, sheetH - interval_V_Leng)
                                                };
                                            }
                                        }
                                    }
                                }

                                // Finally add the adjusted vertical results
                                offsets[angleVert]
                                    .AddRange(newVectorV.Select(n => new Vector2d(n.X + offset_V.X, n.Y + offset_V.Y)));
                            }
                            else
                            {
                                // If partial adjustment possible, add columns first, compare for higher quantity
                                var remainY = 0; 
                                if(hVert > 0) remainY = (interval_H_Leng - interval_H[0].Item3) / hVert; // Vertical quantity
                                var remainY2 = 0; 
                                if(wVert > 0) remainY2 = (interval_H_Leng - interval_H[0].Item3) / wVert; // Horizontal quantity
                                var lastCols = 0; 
                                if(hVert > 0) lastCols = interval_V[0].Item3 / hVert; // Part count in last vertical column
                                if (remainY > 0 && remainY == remainY2)
                                {
                                    var newVectors = new List<Vector2d>(); // Save result
                                    if (remainY >= lastCols)
                                    {
                                        // Place all
                                        for (int i = 0; i < lastCols; i++)
                                        {
                                            newVectors.Add(new Vector2d(interval_H[1].Item1 + offset_V.X,
                                                interval_H[1].Item2 + i * hVert + offset_V.Y));
                                        }

                                        int lastVColNum = offsets[angleVert].Count;
                                        offsets[angleVert] = offsets[angleVert].Take(lastVColNum - lastCols).ToList();
                                        curMaxX_V -= wVert;
                                        interval_V = new List<(int, int, int)>
                                        {
                                            (interval_V[0].Item1 - wVert, interval_V[0].Item2, interval_V_Leng),
                                        };

                                        interval_H = new List<(int, int, int)>
                                        {
                                            (interval_H[0].Item1, interval_H[0].Item2,
                                                interval_H[0].Item3 + lastCols * hVert),
                                            (interval_H[1].Item1, interval_H[1].Item2 + hVert,
                                                sheetH - interval_V_Leng - interval_H[0].Item3 - lastCols * hVert)
                                        }; // No gaps left
                                    }
                                    else
                                    {
                                        // Place partial
                                        for (int i = 0; i < remainY; i++)
                                        {
                                            newVectors.Add(new Vector2d(interval_H[1].Item1 + offset_V.X,
                                                interval_H[1].Item2 + i * hVert + offset_V.Y));
                                        }

                                        int lastVColNum = offsets[angleVert].Count;
                                        offsets[angleVert] = offsets[angleVert].Take(lastVColNum - remainY).ToList();
                                    }
                                    // TODO: Finally check if horizontal can be vertical, skip for now

                                    // Finally add the adjusted vertical results
                                    offsets[angleVert].AddRange(newVectors);
                                }
                                else if (remainY2 > 0 && remainY2 > remainY)
                                {
                                    var newVectorsH = new List<Vector2d>(); // Save result
                                    if (remainY2 >= lastCols)
                                    {
                                        // Place all
                                        for (int i = 0; i < lastCols; i++)
                                        {
                                            newVectorsH.Add(new Vector2d(interval_H[1].Item1 + offset_H.X,
                                                interval_H[1].Item2 + i * wVert + offset_H.Y));
                                        }

                                        int lastVColNum = offsets[angleVert].Count;
                                        offsets[angleVert] = offsets[angleVert].Take(lastVColNum - lastCols).ToList();
                                        curMaxX_V -= wVert;
                                        interval_V = new List<(int, int, int)>
                                        {
                                            (interval_V[0].Item1 - wVert, interval_V[0].Item2, interval_V_Leng),
                                        };

                                        interval_H = new List<(int, int, int)>
                                        {
                                            (interval_H[0].Item1, interval_H[0].Item2,
                                                interval_H[0].Item3 + lastCols * wVert),
                                            (interval_H[1].Item1, interval_H[1].Item2 - lastCols * wVert,
                                                sheetH - interval_V_Leng - interval_H[0].Item3 - lastCols * wVert)
                                        }; // No gaps left
                                    }
                                    else
                                    {
                                        // Place partial
                                        for (int i = 0; i < remainY2; i++)
                                        {
                                            newVectorsH.Add(new Vector2d(interval_H[1].Item1 + offset_H.X,
                                                interval_H[1].Item2 + i * wVert + offset_H.Y));
                                        }

                                        int lastVColNum = offsets[angleVert].Count;
                                        offsets[angleVert] = offsets[angleVert].Take(lastVColNum - remainY2).ToList();
                                    }
                                    // TODO: Finally check if horizontal can be vertical, skip for now

                                    // Finally add the adjusted vertical results
                                    offsets[angleHorz].AddRange(newVectorsH);
                                }
                            }
                        }
                    }
                }
                else
                {
                    // 5-2 Swap attempt: horizontal at bottom, vertical at top. Very special case, limited to one row each for now
                    // Example mainId: 297ed1889b488eed019b4e553e021bfa (thickness 25)
                    if (group_V == 1 && group_H == 1 && offsets[angleVert].Count > 2
                        && offsets[angleHorz].Count > 1
                        && curMaxX_V > curMaxX_H && curMaxX_V - 2 * wVert < curMaxX_H)
                    {
                        int oldMaxX = curMaxX_V;
                        // If last two are vertical, try one vertical one horizontal, position opposite to convention (horizontal bottom, vertical top)
                        var newMaxX = Math.Max(curMaxX_V - 2 * wVert + hVert, curMaxX_H + wVert);
                        if (newMaxX < oldMaxX)
                        {
                            offsets[angleVert].Remove(offsets[angleVert].Last());
                            offsets[angleVert].Remove(offsets[angleVert].Last());
                            TryAddOffset(offsets, angleHorz,
                                new Vector2d(interval_V[0].Item1 - 2 * wVert + offset_H.X,
                                    interval_V[0].Item2 + offset_H.Y));
                            TryAddOffset(offsets, angleVert,
                                new Vector2d(interval_H[0].Item1 + offset_V.X,
                                    interval_H[0].Item2 - (hVert - wVert) + offset_V.Y));
                            curMaxX_V = interval_V[0].Item1 - 2 * wVert + hVert;
                            curMaxX_H = interval_H[0].Item1 + wVert;
                        }
                    }
                }

            }
            var num = offsets.Sum(o => o.Value.Count);
            var solution3 = new Dictionary<string, Dictionary<double, List<Vector2d>>>
            {
                [template.SourceKey] = offsets
            };
            var outerContourPoint = new OuterContourPoint(sheetH);
            foreach (var kvp in offsets)
            {
                double rot = kvp.Key;
                List<SingleBinPack.Vector2d> offsetList = kvp.Value;
                if (GeometryUtil.AlmostEqual(0, rot))
                {
                    foreach (var offset0 in offsetList)
                    {
                        var minX = template.OriginalPart.MinX + offset0.X;
                        outerContourPoint.Update(minX, minX + template.OriginalPart.Width,
                            template.OriginalPart.MinY + template.OriginalPart.Height + offset0.Y);
                    }
                }
                else
                {
                    foreach (var offset0 in offsetList)
                    {
                        var minX = template.OriginalPartRot.MinX + offset0.X;
                        outerContourPoint.Update(minX, minX + template.OriginalPartRot.Width,
                            template.OriginalPartRot.MinY + template.OriginalPartRot.Height + offset0.Y);
                    }
                }
            }

            var nestingSolution =
                new NestingSolution(solution3, Math.Max(curMaxX_V, curMaxX_H), num, outerContourPoint);
            return nestingSolution;
        }

        private void optCols(ref List<(int, int, int)> interval_H, int sheetH, int interval_V_Leng,
            ref int curMaxX_H, int wVert, ref int curMaxX_V,
            Dictionary<double, List<Vector2d>> offsets,
            double angleVert, double angleHorz, Vector2d offset_H, Vector2d offset_V,
            ref List<(int, int, int)> interval_V, int hVert, int interval_H_Leng)
        {
            // In case of multiple vertical columns, only adjust the last column each time
            var vectors = new List<Vector2d>();
            bool is_has_remainY = false;
            while (true)
            {
                if (interval_H.Count == 1)
                {
                    if (curMaxX_H + wVert < curMaxX_V - thresholdV)
                    {
                        var last = offsets[angleVert].Last();
                        offsets[angleVert].Remove(last);
                        vectors.Add(new Vector2d(interval_H[0].Item1 + offset_V.X, interval_H[0].Item2 + offset_V.Y));
                        curMaxX_H = interval_H[0].Item1 + wVert;
                        if (interval_V.Count == 1) // Equals 1 means multiple rows are full
                        {
                            // Dig an interval hole
                            interval_V = new List<(int, int, int)>
                            {
                                (interval_V[0].Item1, interval_V[0].Item2, interval_V[0].Item3 - hVert),
                                (interval_V[0].Item1 - wVert, interval_V[0].Item2 + hVert, hVert),
                            };
                        }
                        else // Multiple rows not full
                        {
                            if (interval_V[0].Item3 == hVert) // Only one, remove directly
                            {
                                curMaxX_V -= wVert;
                                interval_V = new List<(int, int, int)>
                                {
                                    (interval_V[1].Item1, interval_V[1].Item2 - hVert, interval_V_Leng)
                                };
                            }
                            else
                            {
                                // Multiple, remove the top one
                                interval_V = new List<(int, int, int)>
                                {
                                    (interval_V[0].Item1, interval_V[0].Item2, interval_V[0].Item3 - hVert),
                                    (interval_V[1].Item1, interval_V[1].Item2 - hVert, interval_V[1].Item3 + hVert),
                                };
                            }
                        }

                        if (interval_H[0].Item3 == hVert ||
                            interval_H_Leng - hVert < hVert) // Whether completely filled or cannot fill further
                        {
                            interval_H = new List<(int, int, int)>
                                { (interval_H[0].Item1 + wVert, interval_H[0].Item2, sheetH - interval_V_Leng) };
                        }
                        else
                        {
                            // Can fill more later
                            is_has_remainY = true;
                            interval_H = new List<(int, int, int)>
                            {
                                (interval_H[0].Item1 + wVert, interval_H[0].Item2, hVert),
                                (interval_H[0].Item1, interval_H[0].Item2 + hVert,
                                    sheetH - interval_V_Leng - hVert),
                            };
                        }
                    }
                    else
                    {
                        break;
                    }
                }
                else
                {
                    if (is_has_remainY && interval_H[1].Item1 + wVert < curMaxX_V - thresholdV)
                    {
                        // Filling remaining gaps
                        var last = offsets[angleVert].Last();
                        offsets[angleVert].Remove(last);
                        vectors.Add(new Vector2d(interval_H[1].Item1 + offset_V.X, interval_H[1].Item2 + offset_V.Y));
                        if (interval_H[1].Item3 == hVert ||
                            interval_H_Leng - interval_H[0].Item3 - hVert < hVert) // Whether completely filled or cannot fill further
                        {
                            interval_H = new List<(int, int, int)>
                                { (interval_H[0].Item1, interval_H[0].Item2, sheetH - interval_V_Leng) };
                        }
                        else
                        {
                            // Can continue filling
                            interval_H = new List<(int, int, int)>
                            {
                                (interval_H[0].Item1, interval_H[0].Item2, interval_H[0].Item3 + hVert),
                                (interval_H[1].Item1, interval_H[1].Item2 + hVert,
                                    interval_H_Leng - interval_H[0].Item3 - hVert),
                            };
                        }

                        if (interval_V.Count == 1) // Equals 1 means multiple rows are full
                        {
                            // Dig an interval hole
                            interval_V = new List<(int, int, int)>
                            {
                                (interval_V[0].Item1, interval_V[0].Item2, interval_V[0].Item3 - hVert),
                                (interval_V[0].Item1 - wVert, interval_V[0].Item2 + hVert, hVert),
                            };
                        }
                        else // Multiple rows not full
                        {
                            if (interval_V[0].Item3 == hVert) // Only one, remove directly
                            {
                                curMaxX_V -= wVert;
                                interval_V = new List<(int, int, int)>
                                {
                                    (interval_V[1].Item1, interval_V[1].Item2 - hVert, interval_V_Leng)
                                };
                            }
                            else
                            {
                                // Multiple, remove the top one
                                interval_V = new List<(int, int, int)>
                                {
                                    (interval_V[0].Item1, interval_V[0].Item2, interval_V[0].Item3 - hVert),
                                    (interval_V[1].Item1, interval_V[1].Item2 - hVert, interval_V[1].Item3 + hVert),
                                };
                            }
                        }
                    }
                    else
                    {
                        break;
                    }
                }
            }

            if (vectors.Count > 0)
            {
                offsets[angleVert].AddRange(vectors);
            }
        }

        private void TryAddOffset(Dictionary<double, List<Vector2d>> offsets, double rot, Vector2d offset)
        {
            if (!offsets.TryGetValue(rot, out var list))
            {
                list = new List<Vector2d>();
                offsets[rot] = list;
            }

            list.Add(offset);
        }

        private bool IsBetterSolution(NestingSolution newSol, NestingSolution bestSol)
        {
            if (newSol == null) return false;
            if (bestSol == null) return true;

            // Prioritize quantity (must place all)
            if (newSol.Quatity > bestSol.Quatity) return true;
            if (newSol.Quatity < bestSol.Quatity) return false;

            // Then compare MaxX (shorter is better, i.e., more to the left)
            if (newSol.MaxX < bestSol.MaxX) return true;

            return false;
        }

        // =======================================================================
        // Internal Structures (Support for ported logic)
        // =======================================================================

        private struct Vector2d
        {
            public double X;
            public double Y;
            public Vector2d(double x, double y) { X = x; Y = y; }
        }

        // Simulate original Part object in code
        private class MockPart
        {
            public int MinX;
            public int MinY;
            public int Width;
            public int Height;

            public MockPart(int x, int y, int w, int h)
            {
                MinX = x; MinY = y; Width = w; Height = h;
            }
        }

        private class PartItem
        {
            public string SourceKey;
            public int Width;
            public int Height;
            public MockPart OriginalPart;
            public MockPart OriginalPartRot;
        }

        private class NestingSolution
        {
            public Dictionary<string, Dictionary<double, List<Vector2d>>> Solution;
            public int MaxX;
            public int Quatity;
            public OuterContourPoint OuterContour;
            public List<NestingSolution> HistorySolutions;

            public NestingSolution() { }

            public NestingSolution(Dictionary<string, Dictionary<double, List<Vector2d>>> sol, int maxX, int qty, OuterContourPoint contour)
            {
                Solution = sol;
                MaxX = maxX;
                Quatity = qty;
                OuterContour = contour;
            }

            public NestingSolution Clone()
            {
                return (NestingSolution)this.MemberwiseClone();
            }
        }

        private class OuterContourPoint
        {
            private int _sheetHeight;
            public int CurrentMaxX { get; private set; } = 0;

            public OuterContourPoint(int sheetHeight)
            {
                _sheetHeight = sheetHeight;
            }

            public void Update(double minX, double maxX, double maxY)
            {
                if (maxX > CurrentMaxX) CurrentMaxX = (int)maxX;
            }
        }

        private static class GeometryUtil
        {
            public static bool AlmostEqual(double a, double b, double epsilon = 0.001)
            {
                return Math.Abs(a - b) < epsilon;
            }
        }
    }
}