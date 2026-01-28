using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RectangleBinPacking; // 引用你之前的库

namespace SingleBinPackTester
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("=== 单零件排版算法逻辑验证 ===");

            // 测试场景 1: 标准情况 (大板材，小零件)
            RunTest("Case 1_Standard", binW: 6000, binH: 1500, partW: 382, partH: 140, qty: 333);

            // 测试场景 2: 需要旋转的情况 (零件高大于宽，需要旋转适配)
            RunTest("Case 2_Rotation", binW: 12000, binH: 2200, partW: 420, partH: 185, qty: 71);

            // 测试场景 3: 极限填充 (尝试填满)
            RunTest("Case 3_FullFill", binW: 8000, binH: 2200, partW: 330, partH: 250, qty: 102);

            // 测试场景 4: 混合排版 (触发算法中的 MixHV 逻辑)
            // 这是一个比较刁钻的尺寸，横竖结合可能更优
            RunTest("Case 4_ComplexMix", binW: 1000, binH: 1000, partW: 310, partH: 210, qty: 50);

            Console.WriteLine("\n所有测试完成。请查看输出目录下的 SVG 图片验证视觉效果。");
            Console.ReadKey();
        }

        static void RunTest(string testName, int binW, int binH, int partW, int partH, int qty)
        {
            Console.WriteLine($"\n--- 正在测试: {testName} ---");
            Console.WriteLine($"板材: {binW}x{binH}, 零件: {partW}x{partH}, 请求数量: {qty}");

            try
            {
                // 1. 调用算法
                var packer = new SingleBinPack(binW, binH);
                System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
                sw.Start();
                List<Rect> results = packer.Insert(partW, partH, qty);
                sw.Stop();

                // 2. 逻辑验证
                bool pass = true;
                
                // 验证 A: 数量
                Console.WriteLine($"[结果] 已排数量: {results.Count} / {qty}");
                Console.WriteLine($"[耗时] {sw.Elapsed.TotalMilliseconds:F2} ms");

                if (results.Count == 0)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("[警告] 未排入任何零件 (如果是预期放不下则正常)");
                    Console.ResetColor();
                }

                // 验证 B: 边界
                if (!CheckBoundaries(results, binW, binH))
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("[失败] 边界检查不通过！有零件超出了板材。");
                    pass = false;
                    Console.ResetColor();
                }

                // 验证 C: 重叠
                if (!CheckOverlaps(results))
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("[失败] 重叠检查不通过！发现零件重叠。");
                    pass = false;
                    Console.ResetColor();
                }

                // 计算利用率
                double usedArea = results.Sum(r => (long)r.Width * r.Height);
                double totalArea = (long)binW * binH;
                double efficiency = (usedArea / totalArea) * 100;
                Console.WriteLine($"[统计] 利用率: {efficiency:F2}%");

                if (pass)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("[通过] 逻辑验证成功。");
                    Console.ResetColor();
                }

                // 3. 生成可视化 SVG
                GenerateSvg(testName, binW, binH, results);
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[异常] 算法运行出错: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                Console.ResetColor();
            }
        }

        // --- 验证逻辑 ---

        static bool CheckBoundaries(List<Rect> rects, int binW, int binH)
        {
            foreach (var r in rects)
            {
                if (r.X < 0 || r.Y < 0 || r.Right > binW || r.Bottom > binH)
                {
                    Console.WriteLine($"  -> 越界零件: X={r.X}, Y={r.Y}, W={r.Width}, H={r.Height}, Right={r.Right}, Bottom={r.Bottom}");
                    return false;
                }
            }
            return true;
        }

        static bool CheckOverlaps(List<Rect> rects)
        {
            for (int i = 0; i < rects.Count; i++)
            {
                for (int j = i + 1; j < rects.Count; j++)
                {
                    if (IsOverlapping(rects[i], rects[j]))
                    {
                        Console.WriteLine($"  -> 重叠发生: Index[{i}] vs Index[{j}]");
                        Console.WriteLine($"     Rect1: {rects[i].X},{rects[i].Y} {rects[i].Width}x{rects[i].Height}");
                        Console.WriteLine($"     Rect2: {rects[j].X},{rects[j].Y} {rects[j].Width}x{rects[j].Height}");
                        return false;
                    }
                }
            }
            return true;
        }

        static bool IsOverlapping(Rect r1, Rect r2)
        {
            // 标准的 AABB 重叠检测
            return r1.X < r2.Right && r1.Right > r2.X &&
                   r1.Y < r2.Bottom && r1.Bottom > r2.Y;
        }

        // --- 可视化辅助 ---

        static void GenerateSvg(string name, int w, int h, List<Rect> rects)
        {
            string filename = $"{name}_{DateTime.Now:HHmmss}.html";
            using (StreamWriter sw = new StreamWriter(filename))
            {
                // HTML 包装以便直接浏览器打开
                sw.WriteLine("<!DOCTYPE html><html><body>");
                sw.WriteLine($"<h3>{name} - Util: {(rects.Sum(r=>(long)r.Width*r.Height)/(double)((long)w*h)*100):F2}%</h3>");
                
                // SVG 缩放 (如果板材太大，限制显示大小)
                double scale = 1.0;
                if (w > 1000) scale = 1000.0 / w;
                
                sw.WriteLine($"<svg width='{w * scale}' height='{h * scale}' viewBox='0 0 {w} {h}' style='border:1px solid black; background:#f0f0f0;'>");

                int idx = 0;
                foreach (var r in rects)
                {
                    // 随机颜色区分横竖
                    string color = r.Width > r.Height ? "#4CAF50" : "#2196F3"; 
                    string stroke = "#000";
                    
                    sw.WriteLine($"<rect x='{r.X}' y='{r.Y}' width='{r.Width}' height='{r.Height}' " +
                                 $"style='fill:{color};stroke:{stroke};stroke-width:1;opacity:0.8' />");
                    
                    // 在零件中心写序号
                    if (rects.Count < 200) // 数量太多就不写字了
                    {
                        sw.WriteLine($"<text x='{r.X + r.Width / 2}' y='{r.Y + r.Height / 2}' " +
                                     $"font-size='{Math.Min(r.Width, r.Height)/2}' text-anchor='middle' fill='white'>{idx}</text>");
                    }
                    idx++;
                }

                sw.WriteLine("</svg>");
                sw.WriteLine("</body></html>");
            }
            Console.WriteLine($"[可视化] 已生成文件: {filename}");
        }
    }
}