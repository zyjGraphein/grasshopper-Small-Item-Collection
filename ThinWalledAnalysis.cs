// Grasshopper Script Instance

#region Usings

using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;

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

    private void RunScript(
	object Geometry,
	double MaxThickness,
	double SharpAngleDegrees,
	bool Run,
	ref object ColoredMesh,
	ref object ThicknessValues,
	ref object PointsA,
	ref object PointsB)
    {
        // --- 0. 执行开关 ---
        if (!Run)
        {
            Print("脚本未执行。请将 'Run' 设置为 true。");
            return;
        }

        // --- 1. 输入验证与准备 ---
        Print("开始执行高级壁厚分析...");

        if (Geometry == null)
        {
            Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "输入几何体为空。");
            return;
        }

        Mesh inputMesh = null;
        if (Geometry is Brep)
        {
            Brep brep = Geometry as Brep;
            if(!brep.IsSolid)
            {
                Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "输入的Brep不是封闭实体，结果可能不准确。");
            }
            var meshParams = MeshingParameters.Default;
            inputMesh = new Mesh();
            inputMesh.Append(Mesh.CreateFromBrep(brep, meshParams));
            Print($"已将输入的Brep转换为Mesh，包含 {inputMesh.Vertices.Count} 个顶点。");
        }
        else if (Geometry is Mesh)
        {
            inputMesh = (Geometry as Mesh).DuplicateMesh();
            Print($"已接收输入的Mesh，包含 {inputMesh.Vertices.Count} 个顶点。");
        }
        else
        {
            Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "请输入一个有效的Brep或Mesh。");
            return;
        }

        if (MaxThickness <= 0)
        {
            Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "最大检测厚度 (MaxThickness) 必须为正数。");
            return;
        }
        
        double sharpAngleRad = RhinoMath.ToRadians(SharpAngleDegrees);
        Print($"参数设置：最大检测厚度 = {MaxThickness}, 锐角阈值 = {SharpAngleDegrees}°");

        // --- 2. 调用RhinoCommon核心功能进行计算 ---
        Print("正在调用 Mesh.ComputeThickness... 这可能需要一些时间。");
        
        var cancelToken = new CancellationToken();
        MeshThicknessMeasurement[] measurements = Mesh.ComputeThickness(new Mesh[] { inputMesh }, MaxThickness, sharpAngleRad, cancelToken);

        if (measurements == null || measurements.Length == 0)
        {
            Print("分析完成，但未找到任何有效的厚度测量值。请尝试增大'MaxThickness'。");
            Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "未找到厚度测量值。");
            return;
        }
        
        Print($"分析完成！共找到 {measurements.Length} 组厚度测量数据。");

        // --- 3. 处理数据并生成着色网格 ---
        double[] vertexThicknesses = Enumerable.Repeat(-1.0, inputMesh.Vertices.Count).ToArray();
        double minFoundThickness = double.MaxValue;
        double maxFoundThickness = double.MinValue;

        // 遍历所有测量结果
        foreach (var measurement in measurements)
        {
            // *** 最终修正点：使用诊断出的正确属性名称 ***
            double dist = measurement.Thickness;
            int vIndex = measurement.VertexIndex;
            
            // 将厚度值赋给这个顶点
            // 数组中的其他测量值会覆盖另一侧的顶点，从而为所有相关顶点着色
            vertexThicknesses[vIndex] = dist;

            if (dist < minFoundThickness) minFoundThickness = dist;
            if (dist > maxFoundThickness) maxFoundThickness = dist;
        }
        Print($"检测到的厚度范围: 从 {minFoundThickness:F3} 到 {maxFoundThickness:F3}");

        Mesh meshToColor = inputMesh.DuplicateMesh();
        meshToColor.VertexColors.Clear();

        Color[] gradient = new Color[] { Color.FromArgb(0, 0, 255), Color.FromArgb(0, 255, 0), Color.FromArgb(255, 255, 0), Color.FromArgb(255, 0, 0) };

        for (int i = 0; i < meshToColor.Vertices.Count; i++)
        {
            double currentThickness = vertexThicknesses[i];
            Color color;

            if (currentThickness < 0)
            {
                color = Color.Transparent;
                //当前超过max为不识别的区域使用透明颜色，Color.LightGray;则为灰色显示。
            }
            else
            {
                double normalizedValue = 0.0;
                if (maxFoundThickness > minFoundThickness && (maxFoundThickness - minFoundThickness) > 1e-9)
                {
                    normalizedValue = (currentThickness - minFoundThickness) / (maxFoundThickness - minFoundThickness);
                }
                
                if (normalizedValue <= 0) color = gradient[0];
                else if (normalizedValue >= 1) color = gradient[gradient.Length - 1];
                else
                {
                  double colorPos = normalizedValue * (gradient.Length - 1);
                  int index = (int)Math.Floor(colorPos);
                  double factor = colorPos - index;

                  Color c1 = gradient[index];
                  Color c2 = gradient[index + 1];

                  int r = (int)Math.Round(c1.R * (1 - factor) + c2.R * factor);
                  int g = (int)Math.Round(c1.G * (1 - factor) + c2.G * factor);
                  int b = (int)Math.Round(c1.B * (1 - factor) + c2.B * factor);
                  color = Color.FromArgb(r, g, b);
                }
            }
            meshToColor.VertexColors.Add(color);
        }

        // --- 4. 准备输出 ---
        ColoredMesh = meshToColor;
        
        var values = new List<double>();
        var ptsA = new List<Point3d>();
        var ptsB = new List<Point3d>();

        foreach (var m in measurements)
        {
            // *** 最终修正点：使用诊断出的正确属性名称 ***
            values.Add(m.Thickness);
            ptsA.Add(m.Point);
            ptsB.Add(m.OppositePoint); // 使用OppositePoint来获取对面的点
        }

        ThicknessValues = values;
        PointsA = ptsA;
        PointsB = ptsB;
    }
}