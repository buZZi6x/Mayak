using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace LighthouseGame
{
    public static class RadioRuleChecks
    {
        [Serializable] private sealed class Report { public int Passed, Failed; public List<string> Checks = new List<string>(); }
        [Serializable] private sealed class Catalog { public KeeperRadio.Broadcast[] broadcasts; }
        public static void Run()
        {
            var report = new Report();
            void Check(bool result, string name) { if (result) report.Passed++; else report.Failed++; report.Checks.Add((result ? "PASS " : "FAIL ") + name); }
            Check(RadioTuning.Clamp(-100) == 88 && RadioTuning.Clamp(200) == 108, "Tuning stays inside 88–108 MHz");
            Check(RadioTuning.Clamp(float.NaN) == 94.2f && RadioTuning.Clamp(float.PositiveInfinity) == 94.2f, "Invalid input cannot poison audio gains");
            Check(RadioTuning.Band(94.2f) == RadioBand.Coast && RadioTuning.Band(101.6f) == RadioBand.Weather && RadioTuning.Band(88.6f) == RadioBand.Distant, "Three distinct stations");
            Check(RadioTuning.Band(96) == RadioBand.Static && RadioTuning.Signal(96) == 0, "Between stations only static is received");
            Check(RadioTuning.Signal(94.2f) > .999f && RadioTuning.Signal(94.5f) < .41f, "Signal weakens away from the tuned center");
            var text = Resources.Load<TextAsset>("KeeperRadio/Broadcasts");
            Check(text, "Broadcast catalog is bundled");
            var keys = new HashSet<string>();
            if (text)
                foreach (var entry in JsonUtility.FromJson<Catalog>(text.text).broadcasts)
                {
                    var clip = Resources.Load<AudioClip>("KeeperRadio/" + entry.key);
                    Check(keys.Add(entry.key) && !string.IsNullOrWhiteSpace(entry.text) && clip && clip.length > 4 && clip.length < 30,
                        entry.key + " has a unique authored text and playable voice file");
                }
            foreach (var definition in ObservationKernels.Catalog) Check(keys.Contains(definition.Key), "Covered phenomenon: " + definition.Key);
            foreach (var key in new[] { "ghostship", "kraken", "octopus", "feast", "escape", "wreck", "shoal", "ship", "weeping-girl", "leviathan", "lightning", "weather-clear", "weather-cloudy", "weather-fog", "weather-rain", "weather-storm" })
                Check(keys.Contains(key), "Covered event/weather: " + key);
            foreach (var name in new[] { "ashes-of-the-keep", "deckside-jig", "sun-faded-memory" })
            { var clip = Resources.Load<AudioClip>("KeeperRadio/Music/" + name); Check(clip && clip.length > 180, "Complete supplied music: " + name); }
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "../../Artifacts/RadioSleep/rules.json"));
            Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllText(path, JsonUtility.ToJson(report, true));
            if (report.Failed > 0) throw new InvalidOperationException("Radio rules failed: " + path);
            Debug.Log("RADIO_RULES " + report.Passed + " passed");
        }
    }
}
