// Grasshopper Script Instance
#region Usings
using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using Rhino;
using Rhino.Geometry;
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

    /// <summary>
    /// 这个脚本根据一个可自定义的'Search'字符串，
    /// 在'Values'列表中找到匹配项的索引，并用此索引从'Content'数据树中选择对应的分支。
    /// </summary>
    /// <param name="Search">你想要搜索的目标字符串 (设置为 Item Access 和 String Type Hint)。</param>
    /// <param name="Content">输入的数据树 (设置为 Tree Access)。</param>
    /// <param name="Values">输入的任何数据列表 (设置为 List Access 和 String Type Hint)。</param>
    /// <param name="a">输出被选中的列表。</param>
    private void RunScript(
	string Search,
	DataTree<object> Content,
	IList<string> Values,
	ref object a)
    {
        // --- 1. 检查所有输入数据是否有效 ---
        if (string.IsNullOrEmpty(Search))
        {
            Print("错误: 输入端 'Search' 为空。请输入要搜索的字符串。");
            Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "请输入 'Search' 字符串。");
            return;
        }
        if (Content == null || Content.BranchCount == 0)
        {
            Print("错误: 输入端 'Content' 为空或没有数据。");
            Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "输入 'Content' 是无效的树结构。");
            return;
        }
        if (Values == null || Values.Count == 0)
        {
            Print("错误: 输入端 'Values' 为空或没有数据。");
            Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "输入 'Values' 是一个空的列表。");
            return;
        }

        Print("脚本开始执行 (动态字符串查找模式)...");
        Print($"目标搜索字符串: '{Search}'");
        Print($"输入的数据树 'Content' 包含 {Content.BranchCount} 个分支。");
        Print($"输入的列表 'Values' 包含 {Values.Count} 个项目。");

        // --- 2. 查找目标字符串在列表中的索引 ---
        // **核心改动**: 使用 'Search' 输入作为目标字符串, 并对其进行标准化处理（移除空格）
        string targetString = Search.Replace(" ", "");
        int targetIndex = -1; 

        for (int i = 0; i < Values.Count; i++)
        {
            if (Values[i] != null)
            {
                // 获取当前字符串并移除所有空格，使其标准化
                string processedString = Values[i].Replace(" ", "");
                
                // 比较处理后的字符串是否与目标相符
                if (processedString == targetString)
                {
                    targetIndex = i; 
                    Print($"调试信息: 在索引 {i} 处找到匹配字符串 '{Values[i]}'");
                    break; 
                }
            }
        }

        // --- 3. 根据索引选择数据并输出 ---
        if (targetIndex == -1)
        {
            // **核心改动**: 错误信息现在会显示你输入的搜索词
            Print($"调试信息: 在'Values'列表中未找到目标字符串 '{Search}'。");
            Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, $"未找到字符串 '{Search}'。");
            a = null;
        }
        else
        {
            Print($"调试信息: 目标字符串在列表中的最终索引是: {targetIndex}");

            if (targetIndex < Content.BranchCount)
            {
                Print($"正在从 'Content' 中选择第 {targetIndex} 个分支...");
                a = Content.Branch(targetIndex);
                Print("成功选择并输出了指定分支！");
            }
            else
            {
                Print($"错误: 索引 ({targetIndex}) 超出范围。 'Content' 树只有 {Content.BranchCount} 个分支。");
                Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"索引 ({targetIndex}) 超出范围。");
                a = null;
            }
        }
    }
}