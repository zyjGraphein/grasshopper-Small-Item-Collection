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
    private void RunScript(
	bool Run,
	IList<Brep> Breps,
	double Tolerance,
	int Attempts,
	double Jitter,
	ref object Result,
	ref object Fault,
	ref object Log)
    {
        // --- 0. 启动开关检查 ---
        if (!Run)
        {
            Print("Set 'Run' to true to start the operation.");
            return;
        }

        var log = new List<string>();
        var rand = new Random();

        // --- 1. 输入验证 ---
        log.Add("Step 1: Validating inputs...");
        if (Breps == null || Breps.Count < 2)
        {
            log.Add("Input 'Breps' is null or contains fewer than 2 items. Union is not needed.");
            if (Breps != null) Result = Breps;
            Log = log;
            return;
        }

        List<Brep> unprocessedBreps = Breps.Where(b => b != null && b.IsValid).ToList();
        if (unprocessedBreps.Count < 2)
        {
            log.Add("After filtering, there are fewer than 2 valid Breps.");
            Result = unprocessedBreps;
            Log = log;
            return;
        }
        log.Add($"Found {unprocessedBreps.Count} valid Breps to process.");

        if (Tolerance <= 0) { Tolerance = RhinoDoc.ActiveDoc.ModelAbsoluteTolerance; }
        if (Attempts < 0) { Attempts = 0; }
        if (Jitter <= 0) { Jitter = 1.0; }

        // --- 2. 算法初始化 ---
        log.Add("\nStep 2: Initializing algorithm...");
        Brep currentResult = unprocessedBreps[0].DuplicateBrep();
        unprocessedBreps.RemoveAt(0);
        log.Add("Selected the first Brep as the initial seed.");

        // --- 3. 开始“滚雪球”主循环 ---
        int iteration = 0;
        while (unprocessedBreps.Count > 0)
        {
            iteration++;
            log.Add($"\n--- Main Iteration #{iteration} ---");
            log.Add($"Current result has {currentResult.Faces.Count} faces. {unprocessedBreps.Count} Brep(s) remaining.");

            Brep bestCandidate = null;
            bool unionWasPerformedInRecovery = false;

            // **--- 双层搜索逻辑 ---**
            log.Add("Searching for best candidate (Normal Mode: Max Intersection Volume)...");
            
            // --- 常规模式 ---
            double maxIntersectionVolume = -1.0;
            foreach (Brep candidate in unprocessedBreps)
            {
                Brep[] intersection = Brep.CreateBooleanIntersection(currentResult, candidate, Tolerance);
                if (intersection != null && intersection.Length > 0 && intersection.All(b => b != null && b.IsValid))
                {
                    double currentVolume = intersection.Sum(b => b.GetVolume());
                    if (currentVolume > maxIntersectionVolume)
                    {
                        maxIntersectionVolume = currentVolume;
                        bestCandidate = candidate;
                    }
                }
            }
            
            // --- 恢复模式 (仅在常规模式失败时启动) ---
            if (bestCandidate == null)
            {
                log.Add("Normal Mode failed to find an intersecting candidate. Engaging Recovery Mode...");
                
                foreach (Brep recoveryCandidate in unprocessedBreps)
                {
                    log.Add($"  [Recovery] Trying to union with Brep at index {Breps.IndexOf(recoveryCandidate)}...");
                    Brep unionResult = TryUnionWithJitter(currentResult, recoveryCandidate, Attempts, Jitter, Tolerance, rand, log);
                    
                    if (unionResult != null)
                    {
                        log.Add("  [Recovery] SUCCESS! Found a valid union. Returning to Normal Mode.");
                        currentResult = unionResult; // 更新雪球
                        unprocessedBreps.Remove(recoveryCandidate); // 移除已合并的
                        unionWasPerformedInRecovery = true;
                        break; // 恢复成功，退出恢复模式循环
                    }
                }
                
                // 如果恢复模式也失败了，则中止整个流程
                if (!unionWasPerformedInRecovery)
                {
                    log.Add("\n--- PROCESS HALTED: Recovery Mode failed. No more valid unions could be found. ---");
                    Result = currentResult;
                    Fault = unprocessedBreps[0]; // 将第一个剩余的物体作为错误报告
                    Log = String.Join("\n", log);
                    Print("Operation halted due to a boolean failure. Check 'Result' and 'Fault' outputs.");
                    return;
                }
            }

            // 如果是在常规模式下找到了候选者，则在这里进行合并
            if (!unionWasPerformedInRecovery)
            {
                log.Add($"Found best candidate with intersection volume: {maxIntersectionVolume:F4}.");
                Brep unionResult = TryUnionWithJitter(currentResult, bestCandidate, Attempts, Jitter, Tolerance, rand, log);
            
                if (unionResult != null)
                {
                    currentResult = unionResult;
                    unprocessedBreps.Remove(bestCandidate);
                }
                else
                {
                    log.Add($"\n--- PROCESS HALTED: Union failed at Iteration #{iteration}. ---");
                    Result = currentResult;
                    Fault = bestCandidate;
                    Log = String.Join("\n", log);
                    Print("Operation halted due to a boolean failure. Check 'Result' and 'Fault' outputs.");
                    return;
                }
            }
        }

        // --- 4. 最终输出 ---
        log.Add("\n--- Process Finished ---");
        log.Add("All Breps were successfully unioned into a single result.");
        Result = currentResult;
        Fault = null;
        Log = String.Join("\n", log);
        Print("Operation complete. All Breps were successfully unioned.");
    }

    //------------------------------------------------------------------------------------
    // <Custom additional code> (此部分与上一版相同)
    //------------------------------------------------------------------------------------
    private Brep TryUnionWithJitter(Brep brepA, Brep brepB, int maxAttempts, double jitter, double tolerance, Random rand, List<string> log)
    {
        Brep unionAttempt = PerformAndValidateUnion(brepA, brepB, tolerance, log);
        if(unionAttempt != null)
        {
            return unionAttempt;
        }
        
        log.Add("  Union failed validation. Starting jitter attempts...");
        
        for (int i = 1; i <= maxAttempts; i++)
        {
            log.Add($"    Jitter attempt #{i} of {maxAttempts}...");
            
            Brep jitteredCandidate = brepB.DuplicateBrep();
            double moveAmount = 0.01 * jitter;
            double randX = (rand.NextDouble() * 2 - 1) * moveAmount;
            double randY = (rand.NextDouble() * 2 - 1) * moveAmount;
            double randZ = (rand.NextDouble() * 2 - 1) * moveAmount;
            jitteredCandidate.Transform(Transform.Translation(new Vector3d(randX, randY, randZ)));
            
            double scaleDelta = 0.01 * jitter;
            double scaleFactor = (rand.Next(2) == 0) ? (1.0 + scaleDelta) : (1.0 - scaleDelta);
            Point3d center = jitteredCandidate.GetBoundingBox(true).Center;
            jitteredCandidate.Transform(Transform.Scale(center, scaleFactor));
            
            unionAttempt = PerformAndValidateUnion(brepA, jitteredCandidate, tolerance, log);
            if(unionAttempt != null)
            {
                log.Add($"    SUCCESS: Union passed validation on jitter attempt #{i}!");
                return unionAttempt;
            }
        }
        return null;
    }
    private Brep PerformAndValidateUnion(Brep brepA, Brep brepB, double tolerance, List<string> log)
    {
        Brep[] unionResult = Brep.CreateBooleanUnion(new List<Brep> { brepA, brepB }, tolerance);
        if (unionResult == null || unionResult.Length != 1)
        {
            return null;
        }
        Brep resultBrep = unionResult[0];
        if (resultBrep == null || !resultBrep.IsValid)
        {
            return null;
        }
        double volA = brepA.GetVolume();
        double volB = brepB.GetVolume();
        double volResult = resultBrep.GetVolume();
        double volTolerance = 0.0001;
        if (volResult < Math.Max(volA, volB) - volTolerance)
        {
            return null;
        }
        log.Add("  VALIDATION PASSED: Result is a single, valid Brep with a sane volume.");
        return resultBrep;
    }
}