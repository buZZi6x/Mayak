using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace LighthouseGame
{
    public static class LeviathanRuleChecks
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
            var appearance = new LeviathanAppearance();
            for (int day = 1; day <= 5; day++)
            {
                appearance.BeginDay(day, 0);
                Check(report, !appearance.Pending && !appearance.TryConsume(), "Day " + day + " cannot spawn even with roll zero");
            }
            appearance.BeginDay(6, .099999f);
            Check(report, appearance.Pending, "Day 6 accepts a roll just below 10 percent");
            appearance.BeginDay(6, 1);
            Check(report, appearance.Pending, "Waiting does not reroll a successful day");
            Check(report, appearance.TryConsume() && !appearance.TryConsume(), "One selected day produces at most one breach");
            appearance.BeginDay(6, 0);
            Check(report, !appearance.Pending && appearance.Consumed, "Resetting a completed encounter cannot produce another leap");
            appearance.BeginDay(7, .1f);
            for (int i = 0; i < 1000; i++) appearance.BeginDay(7, 0);
            Check(report, !appearance.Pending, "The 10 percent boundary and 1000 retries remain rejected");
            appearance.BeginDay(8, 0);
            Check(report, appearance.Pending && !appearance.Consumed, "The next day gets an independent chance");
            appearance.Reset();
            appearance.BeginDay(1, 0);
            Check(report, appearance.Day == 1 && !appearance.Pending && !appearance.Consumed, "A new run clears late-day state");
            int selected = 0;
            for (int i = 0; i < 1000; i++)
            {
                appearance.Reset(); appearance.BeginDay(6, (i + .5f) / 1000);
                if (appearance.Pending) selected++;
            }
            Check(report, selected == 100, "Exactly 100 of 1000 uniformly spaced rolls spawn");
            foreach (float roll in new[] { -1f, 1f, float.NaN })
            {
                appearance.Reset(); appearance.BeginDay(6, roll);
                Check(report, !appearance.Pending, "Invalid or upper-end roll rejected: " + roll);
            }
            bool submerged = true, spacing = true, continuous = true;
            float maximumY = -1000;
            for (int i = 0; i < LeviathanBreach.BoneCount; i++)
            {
                float offset = i * LeviathanBreach.SpineLength / (LeviathanBreach.BoneCount - 1);
                submerged &= LeviathanBreach.Point(0, offset).y < -30 && LeviathanBreach.Point(LeviathanBreach.Lifetime, offset).y < -30;
            }
            for (float t = 0; t <= LeviathanBreach.Lifetime; t += .05f)
            {
                var point = LeviathanBreach.Point(t);
                maximumY = Mathf.Max(maximumY, point.y);
                continuous &= Vector3.Distance(point, LeviathanBreach.Point(t + .001f)) < .033f;
                for (int i = 1; i < LeviathanBreach.BoneCount; i++)
                {
                    float gap = Vector3.Distance(LeviathanBreach.Point(t, i * 6), LeviathanBreach.Point(t, (i - 1) * 6));
                    spacing &= gap > 5.99f && gap <= 6.001f;
                }
            }
            Check(report, submerged, "The entire spine starts and ends deep underwater");
            Check(report, maximumY > 101 && maximumY < 103, "The breach rises about 102 metres above the water");
            Check(report, continuous && spacing, "Arc-length travel is continuous and preserves spine spacing");
            Check(report, LeviathanBreach.WorldScale == 3 && maximumY * LeviathanBreach.WorldScale > 300,
                "The revised giant is three times larger and breaches over 300 metres high");
            bool shakeContinuous = LeviathanBreach.ShakeEnvelope(0) == 0 && LeviathanBreach.ShakeEnvelope(LeviathanBreach.Lifetime) == 0;
            for (float t = .5f; t < LeviathanBreach.Lifetime; t += .5f) shakeContinuous &= LeviathanBreach.ShakeEnvelope(t) > 0;
            Check(report, shakeContinuous, "Shake spans the whole action and fades to zero at both ends");
            Check(report, LeviathanBreach.ShakeEnvelope(LeviathanBreach.EntryTime) > .9f
                && LeviathanBreach.ShakeEnvelope(LeviathanBreach.DiveTime) > .9f
                && LeviathanBreach.ShakeEnvelope(7) < .4f, "Shake peaks at breach and impact, with softer continuous vibration between them");
            Check(report, LeviathanEncounter.PhotoPoints == 1000 && LeviathanEncounter.PhotoKey == "leviathan", "Photo base reward is 1000 with its own repeat category");
            report.Passed = report.FailedCount == 0;
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "../../Artifacts/Leviathan/rules.json"));
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, JsonUtility.ToJson(report, true));
            if (!report.Passed) throw new InvalidOperationException("Leviathan rule checks failed: " + path);
            Debug.Log("LEVIATHAN_RULES " + report.PassedCount + " passed");
        }

        private static void Check(Report report, bool passed, string label)
        {
            report.Checks.Add((passed ? "PASS " : "FAIL ") + label);
            if (passed) report.PassedCount++; else report.FailedCount++;
        }
    }
}
