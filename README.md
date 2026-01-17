# RectangleBinPack C#

[![NuGet](https://img.shields.io/nuget/v/RectangleBinPack.CSharp.svg)](https://www.nuget.org/packages/RectangleBinPack.CSharp)
[![License](https://img.shields.io/github/license/juj/RectangleBinPack)](https://github.com/juj/RectangleBinPack)

[中文文档 (Chinese)](./README_CN.md)

A complete, high-performance C# port of the famous [RectangleBinPack](https://github.com/juj/RectangleBinPack) library by Jukka Jylänki.

## 📦 Algorithms Included

This library faithfully implements the four core algorithms from the original C++ library:

1.  **MaxRects (MaxRectsBinPack)** - Recommended. Highest packing density.
2.  **Skyline (SkylineBinPack)** - Fastest. Good for runtime packing.
3.  **Guillotine (GuillotineBinPack)** - Simulates edge-to-edge cuts.
4.  **Shelf (ShelfBinPack)** - Simple row-based layout.

## ⚙️ Algorithm Parameters & Heuristics

Different algorithms offer various heuristic strategies. Choosing the right heuristic significantly impacts the packing quality.

### 1. MaxRectsBinPack
Init: `new MaxRectsBinPack(width, height, allowRotations: true)`
Method: `Insert(w, h, FreeRectChoiceHeuristic)`

| Heuristic | Description | Recommendation |
| :--- | :--- | :--- |
| **`RectBestShortSideFit`** | **Best Short Side Fit**. Positions the rectangle to minimize the remaining short side of the free space. | **⭐ Recommended**. Usually yields the best results. |
| `RectBestAreaFit` | **Best Area Fit**. Chooses the smallest free rectangle into which the new one fits. | Good alternative. |
| `RectBottomLeftRule` | **Bottom Left**. Places the rectangle as far down and left as possible (Tetris-style). | Good for intuitive layouts. |
| `RectContactPointRule` | **Contact Point**. Chooses the placement where the rectangle touches other rects as much as possible. | Reduces fragmentation. |
| `RectBestLongSideFit` | **Best Long Side Fit**. Minimizes the remaining long side. | Less commonly used. |

### 2. SkylineBinPack
Init: `new SkylineBinPack(width, height, useWasteMap: true)`
Method: `Insert(w, h, LevelChoiceHeuristic)`

| Heuristic | Description |
| :--- | :--- |
| **`LevelBottomLeft`** | Places the rectangle at the lowest, leftmost position on the skyline. |
| `LevelMinWasteFit` | Places the rectangle where it causes the least "wasted" area (gaps) below it. |

### 3. GuillotineBinPack
Init: `new GuillotineBinPack(width, height)`
Method: `Insert(w, h, merge, FreeRectChoiceHeuristic, GuillotineSplitHeuristic)`
*Requires two strategies: one for choosing the rect, one for splitting the space.*

**A. Choice Strategy (FreeRectChoiceHeuristic):**
* `RectBestAreaFit`: Preferred.
* `RectBestShortSideFit` / `RectBestLongSideFit`
* *(And corresponding "Worst" fits)*

**B. Split Strategy (GuillotineSplitHeuristic):**
* **`SplitMinimizeArea`**: **Recommended**. Splits so the smaller leftover rectangle is minimized (keeping the larger area intact).
* `SplitMaximizeArea`: Splits so the larger leftover rectangle is maximized.
* `SplitShorterLeftoverAxis` / `SplitLongerLeftoverAxis`.

### 4. ShelfBinPack
Init: `new ShelfBinPack(width, height, useWasteMap: false)`
Method: `Insert(w, h, ShelfChoiceHeuristic)`

| Heuristic | Description |
| :--- | :--- |
| **`ShelfNextFit`** | Puts the new rectangle to the last open shelf. Starts a new shelf if it doesn't fit. Fastest. |
| `ShelfBestAreaFit` | Chooses the shelf with smallest remaining shelf area. |
| `ShelfFirstFit` | Tests each shelf and packs into the first one where it fits. |
| `ShelfBestHeightFit` | Chooses the shelf with best-matching height. |

## 🚀 Installation

This library is available on [NuGet](https://www.nuget.org/packages/RectangleBinPack.CSharp).

### .NET CLI
```bash
dotnet add package RectangleBinPack.CSharp
```
Package Manager
```PowerShell
Install-Package RectangleBinPack.CSharp
```
💻 Usage

Basic Example (MaxRects)
```csharp
using System;
using System.Collections.Generic;
using RectangleBinPacking;

public class Program
{
    public static void Main()
    {
        // 1. Initialize a 1024x1024 bin, allowing 90-degree rotations
        var packer = new MaxRectsBinPack(1024, 1024, allowRotations: true);

        // 2. Prepare items to pack
        var items = new List<(int w, int h)> 
        { 
            (200, 100), (50, 50), (300, 300), (1000, 500) 
        };

        // 3. Pack items
        foreach (var item in items)
        {
            Rect result = packer.Insert(item.w, item.h, FreeRectChoiceHeuristic.RectBestShortSideFit);

            if (result.Height > 0)
            {
                Console.WriteLine($"Packed {item.w}x{item.h} at X={result.X}, Y={result.Y}, Rotated={result.Width != item.w}");
            }
            else
            {
                Console.WriteLine($"Failed to pack {item.w}x{item.h}: Bin is full!");
            }
        }
    }
}
```
Advanced Example (Skyline with Waste Map)
```csharp
using System;
using System.Collections.Generic;
using RectangleBinPacking;

public class Program
{
    public static void Main()
    {
        // 1. Initialize a 1024x1024 bin, allowing 90-degree rotations
        var packer = new MaxRectsBinPack(1024, 1024, allowRotations: true);

        // 2. Prepare items to pack
        var items = new List<(int w, int h)> 
        { 
            (200, 100), (50, 50), (300, 300), (1000, 500) 
        };

        // 3. Pack items
        foreach (var item in items)
        {
            Rect result = packer.Insert(item.w, item.h, FreeRectChoiceHeuristic.RectBestShortSideFit);

            if (result.Height > 0)
            {
                Console.WriteLine($"Packed {item.w}x{item.h} at X={result.X}, Y={result.Y}, Rotated={result.Width != item.w}");
            }
            else
            {
                Console.WriteLine($"Failed to pack {item.w}x{item.h}: Bin is full!");
            }
        }
    }
}
```
📄 License

Released under Public Domain (Unlicense) or MIT. You are free to use, modify, and distribute this software for any purpose, commercial or otherwise.