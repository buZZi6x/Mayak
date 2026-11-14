using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace LighthouseGame
{
    public static class BeaconRepairRuleChecks
    {
        [Serializable] private sealed class Report
        {
            public int Passed, Failed;
            public List<string> Checks = new List<string>();
        }

        public static void Run()
        {
            var report = new Report();
            void Check(bool result, string name)
            {
                if (result) report.Passed++; else report.Failed++;
                report.Checks.Add((result ? "PASS " : "FAIL ") + name);
            }

            float lamp = 60, darkness = 0;
            BeaconMaintenance.Tick(ref lamp, ref darkness, 59, false);
            Check(lamp == 1 && darkness == 0, "Healthy operation does not consume the warning deadline");
            BeaconMaintenance.Tick(ref lamp, ref darkness, 1, false);
            Check(lamp == 0 && darkness == 0, "First flash starts with the entire twenty-second window");
            BeaconMaintenance.Tick(ref lamp, ref darkness, 19.9f, false);
            Check(darkness < BeaconMaintenance.WarningSeconds, "Unrepaired beacon survives just before twenty seconds");
            float beforeRepair = darkness;
            BeaconMaintenance.Tick(ref lamp, ref darkness, 6, true);
            Check(lamp == 0 && darkness == beforeRepair, "A full late repair cannot consume any warning time");
            BeaconMaintenance.Tick(ref lamp, ref darkness, .2f, false);
            Check(darkness == BeaconMaintenance.WarningSeconds, "Interrupted repair resumes the remaining deadline and fails at twenty seconds");
            BeaconMaintenance.Tick(ref lamp, ref darkness, 100, false);
            Check(darkness == 20, "Large frames cannot overrun the warning counter");

            lamp = .25f; darkness = 0;
            BeaconMaintenance.Tick(ref lamp, ref darkness, 1, false);
            Check(lamp == 0 && Mathf.Approximately(darkness, .75f), "Frame crossing into warning counts only its warning portion");
            float splitLamp = .25f, splitDarkness = 0;
            for (int i = 0; i < 100; i++) BeaconMaintenance.Tick(ref splitLamp, ref splitDarkness, .01f, false);
            Check(Mathf.Abs(darkness - splitDarkness) < .0001f && splitLamp == lamp, "Warning transition is independent of frame partitioning");
            BeaconMaintenance.Tick(ref lamp, ref darkness, 0, false);
            BeaconMaintenance.Tick(ref lamp, ref darkness, -1, false);
            Check(lamp == 0 && Mathf.Approximately(darkness, .75f), "Paused or negative deltas cannot change the deadline");

            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "../../Artifacts/BeaconRepair/rules.json"));
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, JsonUtility.ToJson(report, true));
            if (report.Failed > 0) throw new InvalidOperationException("Beacon repair rules failed: " + path);
            Debug.Log("BEACON_REPAIR_RULES " + report.Passed + " passed");
        }
    }
}
