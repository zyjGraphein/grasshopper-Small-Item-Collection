#region Usings
using System;
using System.Collections.Generic;
using System.Linq;
using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;
using Rhino.Input;
using Rhino.Input.Custom;
using Grasshopper;
using Grasshopper.Kernel;
#endregion

public class Script_Instance : GH_ScriptInstance
{
  // --- 持久化变量 ---
  private List<GeometryBase> storedGeometry = new List<GeometryBase>();
  private static bool isPicking = false; // 状态锁

  // 用于从RunScript传递参数到Idle事件处理器的成员变量
  private int typeFilter_mem;
  private int countLimit_mem;

  private void RunScript(bool Select, bool Clear, int TypeFilter, int CountLimit, ref object A)
  {
    // --- 清空逻辑 ---
    if (Clear)
    {
      if (storedGeometry.Count > 0)
      {
        storedGeometry.Clear();
        this.Component.ExpireSolution(true);
      }
      return;
    }

    // --- 拾取触发器 ---
    if (Select && !isPicking)
    {
      isPicking = true;
      this.typeFilter_mem = TypeFilter;
      this.countLimit_mem = CountLimit;
      Rhino.RhinoApp.Idle += OnIdle_PickGeometry;
    }

    A = storedGeometry;
  }

  // --- Idle事件处理器：在这里处理预选和后选 ---
  private void OnIdle_PickGeometry(object sender, EventArgs e)
  {
    // 立刻注销事件，确保它只执行一次
    Rhino.RhinoApp.Idle -= OnIdle_PickGeometry;

    try
    {
      List<GeometryBase> pickedGeos = new List<GeometryBase>();
      
      // --- 1. 配置过滤器 ---
      ObjectType filter = ObjectType.AnyObject;
      switch (this.typeFilter_mem)
      {
        case 0: filter = ObjectType.Point; break;
        case 1: filter = ObjectType.Curve; break;
        case 2: filter = ObjectType.Surface | ObjectType.Brep; break;
        case 3: filter = ObjectType.Mesh; break;
      }

      // --- 2. 检查预先选择 ---
      var preSelectedObjects = RhinoDoc.ActiveDoc.Objects.GetSelectedObjects(false, false);
      
      // 筛选出符合类型的预选物体
      foreach (var rhObj in preSelectedObjects)
      {
        if ((rhObj.ObjectType & filter) != 0)
        {
          pickedGeos.Add(rhObj.Geometry.Duplicate());
        }
      }

      // --- 3. 如果没有有效的预选，则启动事后选择 ---
      if (pickedGeos.Count == 0)
      {
        GetObject go = new GetObject();
        go.GeometryFilter = filter;
        string prompt = "Please select geometry";

        if (this.countLimit_mem == 1) // 多选
        {
          go.SetCommandPrompt(prompt + " (multiple allowed)");
          if (go.GetMultiple(1, 0) == GetResult.Object)
          {
            foreach (var objRef in go.Objects())
            {
              if (objRef.Object() != null)
                pickedGeos.Add(objRef.Object().Geometry.Duplicate());
            }
          }
        }
        else // 单选
        {
          go.SetCommandPrompt(prompt + " (single)");
          if (go.Get() == GetResult.Object)
          {
            if (go.Object(0).Object() != null)
              pickedGeos.Add(go.Object(0).Object().Geometry.Duplicate());
          }
        }
      }

      // --- 4. 应用数量限制 (特别是针对预选情况) ---
      if (this.countLimit_mem == 0 && pickedGeos.Count > 1)
      {
        // 如果是单选模式，但预选了多个，则只取第一个
        var firstObject = pickedGeos[0];
        pickedGeos.Clear();
        pickedGeos.Add(firstObject);
        Print("Multiple objects were pre-selected, but only the first one was taken due to single count limit.");
      }

      // --- 5. 更新最终结果 ---
      this.storedGeometry = pickedGeos;
      if(preSelectedObjects.Count() > 0)
      {
          RhinoDoc.ActiveDoc.Objects.UnselectAll();
      }
      Print($"Selection complete. {this.storedGeometry.Count} object(s) stored.");
    }
    catch (Exception ex)
    {
      Rhino.RhinoApp.WriteLine($"An error occurred during picking: {ex.Message}");
    }
    finally
    {
      // 解锁，并强制刷新Grasshopper以显示新结果
      isPicking = false;
      this.Component.ExpireSolution(true);
    }
  }
}