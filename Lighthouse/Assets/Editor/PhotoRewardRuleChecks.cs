using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace LighthouseGame
{
    public static class PhotoRewardRuleChecks
    {
        [Serializable] private sealed class Report
        {
            public bool Passed;
            public int PassedCount, FailedCount;
            public List<string> Checks = new List<string>();
        }

        public static void Run()
        {
            var report = new Report();
            var root = new GameObject("Photo reward rule checks");
            try
            {
                var fleet = root.AddComponent<CoastalFleet>();
                int[] expected = { 60, 80, 100, 120 };
                var keys = new HashSet<string>();
                for (int i = 0; i < expected.Length; i++)
                {
                    fleet.Selected = i;
                    Check(report, fleet.PhotoPoints == expected[i], fleet.VesselName + " = " + expected[i]);
                    keys.Add(fleet.PhotoKey);
                }
                Check(report, keys.Count == 4, "Each vessel class has an independent repeat key");
                Check(report, CoastalGull.PhotoPoints == 20 && WeepingGirlEncounter.PhotoPoints == 500, "Gull and apparition base bonuses are 20 and 500");
                for (int day = 1; day <= 3; day++)
                {
                    var early = new WeepingGirlAppearance();
                    early.BeginDay(day, 0);
                    Check(report, !early.Appears, "Day " + day + " rejects even a successful random roll");
                }
                var appearance = new WeepingGirlAppearance();
                appearance.BeginDay(4, .099f);
                Check(report, appearance.Appears, "Day 4 accepts a roll below 10 percent");
                appearance.BeginDay(4, 1);
                Check(report, appearance.Appears, "A selected day cannot reroll while waiting or resetting the encounter");
                appearance.BeginDay(5, .1f);
                Check(report, !appearance.Appears, "The 10 percent boundary is excluded on the next day");
                for (int i = 0; i < 1000; i++) appearance.BeginDay(5, 0);
                Check(report, !appearance.Appears, "A rejected day remains rejected after 1000 retries");
                appearance.BeginDay(6, 0);
                Check(report, appearance.Appears, "The next day gets an independent chance");
                appearance.Reset();
                appearance.BeginDay(1, 0);
                Check(report, !appearance.Appears && appearance.Day == 1, "A new run clears a previous late-day appearance");
                int selected = 0;
                for (int i = 0; i < 1000; i++)
                {
                    appearance.Reset();
                    appearance.BeginDay(4, (i + .5f) / 1000);
                    if (appearance.Appears) selected++;
                }
                Check(report, selected == 100, "Exactly 100 of 1000 evenly spaced rolls select an eligible day");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
            report.Passed = report.FailedCount == 0;
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "../../Artifacts/PhotoRewards/rules.json"));
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, JsonUtility.ToJson(report, true));
            if (!report.Passed) throw new InvalidOperationException("Photo reward rules failed: " + path);
            Debug.Log("PHOTO_REWARDS_RULES " + report.PassedCount + " passed. " + path);
        }

        private static void Check(Report report, bool passed, string label)
        {
            report.Checks.Add((passed ? "PASS " : "FAIL ") + label);
            if (passed) report.PassedCount++; else report.FailedCount++;
        }
    }
}
