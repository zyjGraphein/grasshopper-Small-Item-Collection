// Grasshopper Script Instance
#region Usings
using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;

using Rhino;
using Rhino.Geometry;
using Rhino.Geometry.Intersect;

using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
#endregion

public class Script_Instance : GH_ScriptInstance
{
    #region Notes
    /* Members:
          RhinoDoc RhinoDocument
          GH_Document GrasshopperDocument
          IGH_Component Component
          int Iteration

        Methods (Virtual & overridable):
          Print(string text)
          Print(string format, params object[] args)
          Reflect(object obj)
          Reflect(object obj, string method_name)
    */
    #endregion

    private void RunScript(IList<Curve> curves, ref object a)
    {
        // --------------------------------------------------------------------------------
        // 1. 输入验证
        // --------------------------------------------------------------------------------
        if (curves == null || curves.Count == 0)
        {
            Print("输入端 'curves' 为空或没有提供任何曲线。");
            return;
        }

        // **---- [代码修正] ----**
        // 输入的 'curves' 是一个 IList<Curve>，它没有 RemoveAll 方法。
        // 我们从它创建一个新的 List<Curve>，以获得完整的列表功能。
        List<Curve> curveList = new List<Curve>(curves);

        // 现在，对我们创建的新列表执行空值移除操作。
        curveList.RemoveAll(c => c == null);
        
        if (curveList.Count == 0)
        {
            Print("输入列表中不包含有效的曲线。");
            return;
        }

        int curveCount = curveList.Count;
        Print($"分析开始：共找到 {curveCount} 条有效曲线。");
        Print("---------------------------------------------");

        // --------------------------------------------------------------------------------
        // 2. 初始化分组所需的数据结构
        // --------------------------------------------------------------------------------
        // `groups` 用于存储最终的分组结果，每个子列表包含一组曲线的索引
        List<List<int>> groups = new List<List<int>>();

        // `visited` 数组用于跟踪每条曲线是否已经被分配到一个组中
        bool[] visited = new bool[curveCount];

        // --------------------------------------------------------------------------------
        // 3. 核心逻辑：遍历并分组
        // --------------------------------------------------------------------------------
        // 遍历每一条曲线，如果它还没有被访问过，就从它开始寻找一个全新的连接组
        for (int i = 0; i < curveCount; i++)
        {
            if (!visited[i])
            {
                // 发现一条未分组的曲线，开始创建一个新组
                Print($"发现未分组曲线，索引: {i}。开始创建新组...");

                List<int> newGroup = new List<int>();
                // 使用队列(Queue)来进行广度优先搜索(BFS)，以找到所有与当前曲线相连的曲线
                Queue<int> queue = new Queue<int>();

                // 将当前曲线索引加入队列，并标记为已访问
                queue.Enqueue(i);
                visited[i] = true;

                // 当队列不为空时，持续处理
                while (queue.Count > 0)
                {
                    // 取出队列中的一个曲线索引进行处理
                    int currentIndex = queue.Dequeue();
                    newGroup.Add(currentIndex); // 将其加入当前的新分组

                    Curve currentCurve = curveList[currentIndex];
                    Print($"  -> 正在处理索引为 {currentIndex} 的曲线...");

                    // 检查这条曲线与所有其他【未被访问过的】曲线是否相交
                    for (int j = 0; j < curveCount; j++)
                    {
                        if (!visited[j]) // 只检查尚未分组的曲线
                        {
                            Curve otherCurve = curveList[j];

                            // 计算两条曲线的交点
                            // 使用文档的绝对公差来确保精度
                            double tolerance = RhinoDocument.ModelAbsoluteTolerance;
                            CurveIntersections events = Intersection.CurveCurve(currentCurve, otherCurve, tolerance, tolerance);

                            // 如果交点数量大于0，说明两条曲线接触
                            if (events.Count > 0)
                            {
                                Print($"     - 发现交点: 曲线 {currentIndex} 与曲线 {j} 相交。");
                                // 将相交的曲线标记为已访问，并加入队列等待处理
                                // 这意味着它也将被添加到当前这个组中
                                visited[j] = true;
                                queue.Enqueue(j);
                            }
                        }
                    }
                }

                // 当队列为空时，意味着与曲线i相关的所有连接曲线都已被找到
                // 将这个完整的新组添加到总的分组列表中
                groups.Add(newGroup);
                Print($"新组创建完毕！该组包含 {newGroup.Count} 条曲线。");
                Print("---------------------------------------------");
            }
        }

        // --------------------------------------------------------------------------------
        // 4. 格式化输出
        // --------------------------------------------------------------------------------
        // 将分组后的索引列表转换为Grasshopper的数据树(DataTree)结构
        DataTree<Curve> outputTree = new DataTree<Curve>();

        for (int i = 0; i < groups.Count; i++)
        {
            GH_Path path = new GH_Path(i); // 为每个组创建一个新的分支
            List<int> groupIndices = groups[i];
            
            // 创建一个临时列表来存放属于当前分支的曲线
            List<Curve> groupCurves = new List<Curve>();
            foreach (int curveIndex in groupIndices)
            {
                groupCurves.Add(curveList[curveIndex]);
            }
            // 将这组曲线一次性添加到数据树的对应分支中
            outputTree.AddRange(groupCurves, path);
        }
        
        Print($"全部分析完成，共创建了 {groups.Count} 个独立的曲线组。");

        // 将最终的数据树赋值给输出参数 a
        a = outputTree;
    }
}