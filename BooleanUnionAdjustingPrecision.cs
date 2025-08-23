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
    /* 
      Members:
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

    private void RunScript(IList<Brep> breps, double tol, ref object result)
    {
        // 执行布尔并集操作
        Brep[] unionResult = Brep.CreateBooleanUnion(breps, tol);
        
        // 打印结果信息
        if (unionResult != null && unionResult.Length > 0)
        {
            Print("布尔并集操作成功");
            Print("结果数量: {0}", unionResult.Length);
        }
        else
        {
            Print("布尔并集操作失败或没有有效结果");
        }
        
        // 输出结果
        result = unionResult;
    }
}
