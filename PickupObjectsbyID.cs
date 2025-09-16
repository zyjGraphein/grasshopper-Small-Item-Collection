// Grasshopper Script Instance
#region Usings
using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using Rhino;
using Rhino.Geometry;
using Rhino.DocObjects;
using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
#endregion

public class Script_Instance : GH_ScriptInstance
{
  #region Notes
  /*
   * Members:
   * RhinoDoc RhinoDocument
   * GH_Document GrasshopperDocument
   * IGH_Component Component
   * int Iteration
   * Methods (Virtual & overridable):
   * Print(string text)
   * Print(string format, params object[] args)
   * Reflect(object obj)
   * Reflect(object obj, string method_name)
   */
  #endregion

  private void RunScript(object id, ref object A)
  {
    // =================================================================================
    // 1. 初始化和输入检查
    // =================================================================================

    Print("脚本开始运行...");
    A = null;

    if (id == null)
    {
      Print("错误：输入端(id)为空，请提供一个有效的GUID。");
      Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "输入端 (id) 为空");
      return;
    }

    // =================================================================================
    // 2. 将输入转换为GUID
    // =================================================================================

    Guid objectGuid;
    try
    {
      if (id is GH_Guid)
      {
        objectGuid = ((GH_Guid)id).Value;
      }
      else
      {
        objectGuid = new Guid(id.ToString());
      }
      Print($"成功解析GUID: {objectGuid}");
    }
    catch (Exception e)
    {
      Print($"错误：无法将输入值转换为有效的GUID。输入值: '{id.ToString()}'");
      Print($"异常信息: {e.Message}");
      Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "输入值不是有效的GUID格式");
      return;
    }

    // =================================================================================
    // 3. 在Rhino文档中查找物件
    // =================================================================================

    Print("正在Rhino文档中查找物件...");
    RhinoObject rhinoObject = RhinoDocument.Objects.FindId(objectGuid);

    // =================================================================================
    // 4. 处理查找结果并输出
    // =================================================================================

    if (rhinoObject != null)
    {
      Print($"成功找到物件！物件ID: {rhinoObject.Id}");
      Print($"物件类型: {rhinoObject.ObjectType}");

      // ▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼ 核心修改点 ▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼
      // 输出物件的几何信息，而不是物件本身这个“容器”
      // 增加一个安全检查，确保几何信息存在
      if(rhinoObject.Geometry != null)
      {
        A = rhinoObject.Geometry;
        Print("已成功提取并输出物件的几何数据 (Geometry)。");
      }
      else
      {
        Print($"警告：找到了物件，但其几何数据 (Geometry) 为空。");
        Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "找到的物件不包含几何数据");
      }
      // ▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲ 核心修改点 ▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲
    }
    else
    {
      Print($"警告：在当前Rhino文档中未找到ID为 {objectGuid} 的物件。");
      Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "在Rhino文档中未找到指定ID的物件");
    }
    
    Print("脚本运行结束。");
  }
}