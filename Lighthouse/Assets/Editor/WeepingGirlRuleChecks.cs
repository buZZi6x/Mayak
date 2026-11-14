using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace LighthouseGame
{
    // Deterministic acceptance checks; intentionally independent of scene/editor state.
    public static class WeepingGirlRuleChecks
    {
        [Serializable]
        private sealed class CheckResult
        {
            public string Name;
            public bool Passed;
            public string Failure;
        }

        [Serializable]
        private sealed class Report
        {
            public bool Done;
            public bool Passed;
            public int PassedCount;
            public int FailedCount;
            public string Scope = "Deterministic gaze rules only; does not establish scene visibility, audio, camera cleanup, or game feel.";
            public List<CheckResult> Checks = new List<CheckResult>();
        }

        public static void Run()
        {
            var report = new Report();

            Check(report, "A 2.9-second look is safe", () =>
            {
                var gaze = new GazeExposure();
                Require(!gaze.Step(2.9f, true, true), "A short look emitted an attack.");
                Require(!gaze.Triggered, "A short look committed the attack.");
                Near(gaze.Seconds, 2.9f, "Valid viewing time was lost.");
            });

            Check(report, "Exactly 3 seconds remains safe; the next positive step commits", () =>
            {
                var gaze = new GazeExposure();
                for (int i = 0; i < 12; i++)
                {
                    Require(!gaze.Step(.25f, true, true), "Attack occurred at or before 3 seconds.");
                }
                Near(gaze.Seconds, 3f, "Threshold setup did not reach exactly 3 seconds.");
                Require(!gaze.Triggered, "The equality boundary committed an attack.");
                Require(gaze.Step(.001f, true, true), "Crossing the boundary did not emit an attack.");
                Require(gaze.Triggered, "Emitted attack was not committed.");
            });

            Check(report, "A deliberate 0.15-second look away clears a near-lethal stare", () =>
            {
                var gaze = new GazeExposure();
                gaze.Step(2.9f, true, true);
                Require(!gaze.Step(.15f, false, true), "Looking away emitted an attack.");
                Near(gaze.Seconds, 0, "A deliberate look away retained previous exposure.");
                Require(!gaze.Step(2.9f, true, true), "A second safe look inherited exposure from the first.");
                Require(!gaze.Triggered, "Two separated short looks committed an attack.");
            });

            Check(report, "A brief boundary tremor pauses exposure without adding time", () =>
            {
                var gaze = new GazeExposure();
                gaze.Step(2.9f, true, true);
                Require(!gaze.Step(.14f, false, true), "A brief look away emitted an attack.");
                Near(gaze.Seconds, 2.9f, "Boundary tremor either added or discarded viewing time.");
                Require(!gaze.Step(.05f, true, true), "Away time was incorrectly counted as looking.");
                Require(gaze.Step(.06f, true, true), "A resumed continuous look did not eventually commit.");
            });

            Check(report, "Separate short interruptions do not combine into one deliberate look away", () =>
            {
                var gaze = new GazeExposure();
                gaze.Step(2.7f, true, true);
                gaze.Step(.1f, false, true);
                gaze.Step(.1f, true, true);
                gaze.Step(.1f, false, true);
                Near(gaze.Seconds, 2.8f, "Non-contiguous away time incorrectly reset exposure.");
                Require(gaze.Step(.21f, true, true), "Repeated tiny interruptions bypassed the stare limit.");
            });

            Check(report, "A multi-frame deliberate look away resets at the boundary", () =>
            {
                var gaze = new GazeExposure();
                gaze.Step(2.9f, true, true);
                gaze.Step(.075f, false, true);
                Near(gaze.Seconds, 2.9f, "First half of the reset window cleared exposure early.");
                gaze.Step(.075f, false, true);
                Near(gaze.Seconds, 0, "A complete multi-frame reset window failed to clear exposure.");
                Require(!gaze.Step(.11f, true, true), "Old exposure survived the reset window.");
            });

            Check(report, "A long look while ineligible cannot trigger or bank exposure", () =>
            {
                var gaze = new GazeExposure();
                Require(!gaze.Step(30f, true, false), "Ineligible viewing emitted an attack.");
                Require(!gaze.Triggered, "Ineligible viewing committed an attack.");
                Near(gaze.Seconds, 0, "Ineligible viewing banked time.");
                Require(!gaze.Step(2.9f, true, true), "Becoming eligible inherited ineligible viewing time.");
            });

            Check(report, "Losing eligibility clears pre-trigger exposure immediately", () =>
            {
                var gaze = new GazeExposure();
                gaze.Step(2.9f, true, true);
                gaze.Step(.001f, true, false);
                Near(gaze.Seconds, 0, "Preparation/sleep/inspection transition retained exposure.");
                Require(!gaze.Step(2.9f, true, true), "Regaining eligibility inherited previous exposure.");
            });

            Check(report, "Zero time freezes both the stare and the look-away window", () =>
            {
                var gaze = new GazeExposure();
                gaze.Step(2.9f, true, true);
                gaze.Step(.1f, false, true);
                for (int i = 0; i < 100; i++)
                {
                    Require(!gaze.Step(0, true, true), "Paused viewing emitted an attack.");
                    Require(!gaze.Step(0, false, true), "Paused look away emitted an attack.");
                }
                Near(gaze.Seconds, 2.9f, "Paused calls changed viewing time.");
                gaze.Step(.049f, false, true);
                Near(gaze.Seconds, 2.9f, "Pause advanced the look-away reset window.");
                gaze.Step(.002f, false, true);
                Near(gaze.Seconds, 0, "Resumed look-away window failed to finish.");
            });

            Check(report, "A non-positive time step cannot create or rewind exposure", () =>
            {
                var gaze = new GazeExposure();
                gaze.Step(2.9f, true, true);
                Require(!gaze.Step(-1f, true, true), "Negative time emitted an attack.");
                Near(gaze.Seconds, 2.9f, "Negative time rewound exposure.");
                Require(!gaze.Step(0, true, true), "Zero time emitted an attack.");
                Require(gaze.Step(.11f, true, true), "Non-positive steps damaged subsequent threshold crossing.");
            });

            Check(report, "Committed exposure emits exactly one attack despite further updates", () =>
            {
                var gaze = new GazeExposure();
                int attacks = gaze.Step(3.01f, true, true) ? 1 : 0;
                for (int i = 0; i < 600; i++)
                {
                    if (gaze.Step(1f / 60f, true, true)) attacks++;
                }
                Require(attacks == 1, "Expected one attack, received " + attacks + ".");
                Require(gaze.Triggered, "Commitment was lost during subsequent updates.");
            });

            Check(report, "Looking away after commitment cannot cancel the attack", () =>
            {
                var gaze = new GazeExposure();
                gaze.Step(3.01f, true, true);
                Require(!gaze.Step(5f, false, true), "Looking away emitted a second attack.");
                Require(gaze.Triggered, "Looking away cancelled a committed attack.");
                Require(!gaze.Step(5f, true, true), "Looking back emitted a second attack.");
            });

            Check(report, "Explicit reset clears a committed attack and rearms a fresh run", () =>
            {
                var gaze = new GazeExposure();
                Require(gaze.Step(3.01f, true, true), "First run did not commit.");
                gaze.Reset();
                Require(!gaze.Triggered, "Restart retained commitment.");
                Near(gaze.Seconds, 0, "Restart retained exposure.");
                Require(!gaze.Step(2.9f, true, true), "Fresh run triggered before its own full exposure.");
                Require(gaze.Step(.11f, true, true), "Fresh run was not rearmed.");
            });

            Check(report, "Reset also clears a partially elapsed look-away window", () =>
            {
                var gaze = new GazeExposure();
                gaze.Step(2.9f, true, true);
                gaze.Step(.14f, false, true);
                gaze.Reset();
                gaze.Step(.02f, false, true);
                gaze.Step(2.9f, true, true);
                gaze.Step(.02f, false, true);
                Near(gaze.Seconds, 2.9f, "Previous run's away window contaminated the fresh run.");
                Require(gaze.Step(.11f, true, true), "Fresh run inherited stale away state.");
            });

            foreach (int framesPerSecond in new[] { 30, 60, 120, 144 })
            {
                int cadence = framesPerSecond;
                Check(report, "The same short-look/attack outcome holds at " + cadence + " FPS", () =>
                {
                    var gaze = new GazeExposure();
                    float step = 1f / cadence;
                    int earlyFrames = (int)Math.Floor(2.9 * cadence);
                    for (int i = 0; i < earlyFrames; i++)
                    {
                        Require(!gaze.Step(step, true, true), "Frame cadence caused a sub-2.9-second attack.");
                    }
                    int attacks = 0;
                    int extraFrames = (int)Math.Ceiling(.2 * cadence);
                    for (int i = 0; i < extraFrames; i++)
                    {
                        if (gaze.Step(step, true, true)) attacks++;
                    }
                    Require(attacks == 1 && gaze.Triggered, "Crossing 3 seconds at this cadence did not emit exactly one attack.");
                });
            }

            report.Done = true;
            report.Passed = report.FailedCount == 0;
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "Artifacts", "WeepingGirlV2", "rules.json"));
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, JsonUtility.ToJson(report, true), new UTF8Encoding(false));
            if (!report.Passed)
            {
                throw new InvalidOperationException("Weeping girl rules failed " + report.FailedCount + " checks. See " + path);
            }
            Debug.Log("Weeping girl deterministic rules: " + report.PassedCount + "/" + report.Checks.Count + " passed. " + path);
        }

        private static void Check(Report report, string name, Action action)
        {
            var result = new CheckResult { Name = name };
            try
            {
                action();
                result.Passed = true;
                report.PassedCount++;
            }
            catch (Exception exception)
            {
                result.Failure = exception.Message;
                report.FailedCount++;
            }
            report.Checks.Add(result);
        }

        private static void Near(float actual, float expected, string message)
        {
            Require(Math.Abs(actual - expected) <= .0001f, message + " Expected " + expected + ", actual " + actual + ".");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
