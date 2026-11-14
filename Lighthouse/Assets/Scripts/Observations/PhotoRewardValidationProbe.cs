#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace LighthouseGame
{
    // Attach only in Play Mode. All staging disappears when Play Mode stops.
    public sealed class PhotoRewardValidationProbe : MonoBehaviour
    {
        public bool Done, Passed;
        public string Stage;
        private BlockoutSession _session;
        private BlockoutPlayer _player;
        private BlockoutDirector _director;
        private WeepingGirlEncounter _girl;
        private CoastalGull[] _gulls;
        private string _output;
        private readonly List<string> _checks = new List<string>();
        private int _record;
        private bool _hadRecord;

        [Serializable] private sealed class Report
        {
            public bool Done, Passed;
            public string[] Checks;
        }

        private void Check(bool passed, string label)
        {
            _checks.Add((passed ? "PASS " : "FAIL ") + label);
            Debug.Log("PHOTO_REWARDS " + _checks[_checks.Count - 1]);
        }

        private IEnumerator Start()
        {
            _output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../Artifacts/PhotoRewards"));
            Directory.CreateDirectory(_output);
            _session = FindAnyObjectByType<BlockoutSession>();
            _player = _session.Player;
            _director = _session.Director;
            _girl = FindAnyObjectByType<WeepingGirlEncounter>();
            _gulls = FindObjectsByType<CoastalGull>();
            _hadRecord = PlayerPrefs.HasKey("Lighthouse.Blockout.Best");
            _record = PlayerPrefs.GetInt("Lighthouse.Blockout.Best", 0);
            _player.enabled = false;
            _player.InputEnabled = false;
            _player.LockCursor(false);
            var environment = CinematicEnvironment.Active;
            environment.AdvanceTime = false;
            environment.SetWeather(CoastalWeather.Clear, true);
            environment.SetHour(16);
            foreach (var gull in _gulls) { gull.enabled = false; gull.gameObject.SetActive(false); }
            ResetPhotos();
            FreezeSea();
            yield return null;

            Stage = "Vessel classes and independent repeats";
            var fleet = _director.Traffic.Ships[0];
            fleet.gameObject.SetActive(true);
            fleet.transform.SetPositionAndRotation(new Vector3(150, .5f, 0), Quaternion.identity);
            Aim(new Vector3(150, 12, -65), new Vector3(150, 10, 0));
            int[] prices = { 60, 80, 100, 120 };
            for (int i = 0; i < 4; i++)
            {
                fleet.Select(i);
                yield return null;
                _director.PhotoTarget(_player.View, out string key, out string label, out int basic);
                Check(key == fleet.PhotoKey && basic == prices[i] && label == fleet.VesselName, "Visible vessel class " + i + " uses its own label and price");
                yield return Capture("vessel-" + i);
                var shot = _session.Shots.Last();
                Check(shot.Price == prices[i] && shot.Subjects.Count == 1, "First photo of class " + i + " retains full value after other classes");
            }
            fleet.Select(0);
            yield return Capture(null);
            Check(_session.Shots[4].Price == 30 && _session.Shots[0].Price == 60 && _session.Score == 390,
                "Repeating the cutter halves only its reward and preserves previous photos");
            _session.BusyUntil = 0; _session.Capture();
            Check(_session.Shots.Count == 5 && _session.Score == 390, "The five-photo quota still blocks a sixth photo");
            var secondFleet = _director.Traffic.Ships[1];
            secondFleet.gameObject.SetActive(true); secondFleet.Select(3);
            secondFleet.transform.SetPositionAndRotation(new Vector3(174, .5f, 0), Quaternion.identity);
            Aim(new Vector3(162, 15, -95), new Vector3(162, 10, 0));
            yield return null;
            _director.PhotoTarget(_player.View, out string multipleKey, out _, out int multiplePrice);
            Check(PhotoEvidence.Vessel(_player.View, fleet.transform) && PhotoEvidence.Vessel(_player.View, secondFleet.transform)
                && multipleKey == secondFleet.PhotoKey && multiplePrice == 120, "Two visible ships select one most valuable class");
            fleet.gameObject.SetActive(false); secondFleet.gameObject.SetActive(false);
            Check(!PhotoEvidence.Vessel(_player.View, secondFleet.transform), "An inactive vessel is not photographic evidence");

            Stage = "Gull geometry and composite photos";
            ResetPhotos(); FreezeSea();
            var bird = _gulls[0]; bird.gameObject.SetActive(true);
            bird.transform.SetPositionAndRotation(new Vector3(150, 8, 0), Quaternion.identity);
            Aim(new Vector3(150, 8.25f, -4), bird.transform.position + Vector3.up * .2f);
            yield return null;
            Check(bird.VisibleIn(_player.View), "A real posed gull is photographable");
            yield return Capture("gull");
            Check(_session.Shots[0].Key == "gulls" && _session.Shots[0].Price == 20, "Gull alone awards 20 and labels the photo");
            var secondBird = _gulls[1]; secondBird.gameObject.SetActive(true);
            secondBird.transform.SetPositionAndRotation(bird.transform.position + Vector3.right * .5f, Quaternion.identity);
            yield return Capture(null);
            Check(_session.Shots[1].Price == 10 && _session.Shots[1].Subjects.Count == 1, "Several gulls give one independently discounted bonus per frame");
            Aim(_player.View.transform.position, _player.View.transform.position + Vector3.back);
            Check(!bird.VisibleIn(_player.View), "A gull behind the camera gives no evidence");
            Aim(new Vector3(150, 8.25f, -4), bird.transform.position + Vector3.up * .2f);
            var wall = Barrier(_player.View.transform.position, bird.transform.position + Vector3.up * .2f, 3);
            Check(!bird.VisibleIn(_player.View), "An opaque wall blocks gull credit");
            wall.SetActive(false); Destroy(wall);
            var birdBody = bird.GetComponentInChildren<SkinnedMeshRenderer>();
            birdBody.enabled = false;
            Check(!bird.VisibleIn(_player.View), "An invisible gull renderer gives no credit");
            birdBody.enabled = true;
            secondBird.gameObject.SetActive(false);
            ResetPhotos(); FreezeSea();
            fleet.gameObject.SetActive(true); fleet.Select(3);
            fleet.transform.SetPositionAndRotation(new Vector3(150, .5f, 0), Quaternion.identity);
            Aim(new Vector3(150, 12, -65), new Vector3(150, 10, 0));
            bird.transform.position = _player.View.transform.position + _player.View.transform.forward * 4 + _player.View.transform.right * 1.6f;
            yield return Capture("steamer-and-gull");
            Check(_session.Shots[0].Price == 140 && _session.Shots[0].Subjects.Count == 2, "Steamer plus gull totals 120 + 20");
            yield return Capture(null);
            Check(_session.Shots[1].Price == 70 && _session.Shots[0].Price == 140, "Both subjects discount independently; the original stays 140");
            fleet.gameObject.SetActive(false); bird.gameObject.SetActive(false);

            Stage = "Rare apparition scheduling and actual photos";
            ResetPhotos(); FreezeSea();
            _session.Running = true; _session.Preparation = 0;
            _session.Fuel = 1000; _session.Lamp = 1000;
            for (int day = 1; day <= 3; day++)
            {
                _session.Day = day;
                AimGirl(false);
                yield return null; yield return null;
                AimGirl(true);
                Check(!_girl.IsVisible && !_girl.VisibleIn(_player.View), "No apparition or photo credit on day " + day);
            }
            SelectDay(4, 0);
            AimGirl(true);
            yield return null; yield return null;
            Check(!_girl.IsVisible, "A successful day-4 roll waits while the spawn point is being watched");
            AimGirl(false);
            yield return null; yield return null;
            AimGirl(true);
            yield return null;
            Check(_girl.IsVisible && _girl.VisibleIn(_player.View), "Day-4 apparition becomes visible after offscreen admission");
            environment.SetWeather(CoastalWeather.Clear, true); environment.SetHour(16);
            yield return new WaitForSeconds(.8f);
            // The ordinary photo frame is wider than the dangerous central gaze cone.
            _player.View.transform.rotation *= Quaternion.Euler(0, 18, 0);
            Check(_girl.VisibleIn(_player.View) && !_girl.IsLookedAt(_player.View), "A readable apparition off-center still counts without a direct stare");
            AimGirl(true);
            yield return Capture("weeping-girl");
            Check(_session.Shots[0].Key == "weeping-girl" && _session.Shots[0].Price == 500, "Apparition alone awards 500 points");
            yield return Capture(null);
            Check(_session.Shots[1].Price == 250 && _session.Shots[0].Price == 500, "Apparition repeat awards 250 without repricing the first photo");
            wall = Barrier(_player.View.transform.position, _girl.SeatedFocus, 3);
            Check(!_girl.VisibleIn(_player.View), "A wall blocks apparition photo credit");
            wall.SetActive(false); Destroy(wall);
            _session.Sleeping = true;
            yield return null;
            Check(!_girl.IsVisible && !_girl.VisibleIn(_player.View), "Sleep hides the apparition and blocks its reward");
            _session.Sleeping = false;
            SelectDay(5, .9f); AimGirl(false);
            for (int i = 0; i < 20; i++) { _girl.ResetEncounter(); yield return null; }
            Check(!_girl.IsVisible, "A failed daily roll stays absent after repeated resets and waiting");
            SelectDay(6, 0); AimGirl(false);
            yield return null; yield return null;
            AimGirl(true);
            Check(_girl.IsVisible, "A later successful day can show the apparition again");
            _session.BeginRun(); FreezeSea();
            yield return null;
            Check(_session.Day == 1 && !_girl.IsVisible && !_player.EncounterControlLocked, "Restart returns to day 1 with no old apparition or control lock");

            Stage = "Real bed transition resets new category discounts";
            _session.Preparation = 0; _session.Fuel = 1000; _session.Lamp = 1000;
            _session.Day = 3;
            for (int i = 0; i < 5; i++) _session.Shots.Add(new BlockoutShot { Sent = true });
            var repeats = (Dictionary<string, int>)typeof(BlockoutSession).GetField("repeats", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(_session);
            repeats["gulls"] = 2; repeats["weeping-girl"] = 1; repeats["boat-frigate"] = 1;
            var bed = FindObjectsByType<BlockoutInteractable>().First(s => s.Kind == BlockoutInteractable.Station.Bed);
            _session.BusyUntil = 0; _session.Work(bed);
            float deadline = Time.realtimeSinceStartup + 8;
            // The day advances before the wake-up fade ends; admission still waits for Sleeping to clear.
            while ((_session.Day == 3 || _session.Sleeping) && Time.realtimeSinceStartup < deadline) yield return null;
            Check(_session.Day == 4 && !_session.Sleeping && _session.Shots.Count == 0 && _session.Price("gulls", 20) == 20
                && _session.Price("weeping-girl", 500) == 500 && _session.Price("boat-frigate", 80) == 80,
                "Sleeping from day 3 to 4 clears photo slots and all new repeat counters");
            yield return null;
            Check(Appearance.Day == 4, "The real day transition evaluates the apparition schedule");

            Stage = "Photograph before the dangerous stare threshold";
            SelectDay(5, 0); AimGirl(false);
            yield return null; yield return null;
            AimGirl(true);
            _player.InputEnabled = true; _player.LockCursor(true);
            deadline = Time.realtimeSinceStartup + 6;
            while (_girl.Exposure < 2.1f && Time.realtimeSinceStartup < deadline) yield return null;
            Check(_girl.Exposure >= 2.1f && !_girl.IsCommitted, "A short look remains safe on an eligible day");
            _session.BusyUntil = 0; _session.Capture();
            yield return new WaitForEndOfFrame(); yield return null;
            Check(_session.Shots.Count == 1 && _session.Shots[0].Price == 500,
                "The apparition can be photographed for 500 before the stare commits");
            while (!_girl.IsCommitted && Time.realtimeSinceStartup < deadline) yield return null;
            Check(_girl.IsCommitted && _player.EncounterControlLocked, "Looking longer than three seconds still commits and locks controls");
            _session.BusyUntil = 0; _session.Capture();
            Check(_session.Shots.Count == 1, "A committed attack blocks further photographs");
            _session.BeginRun();
            Check(!_player.EncounterControlLocked && !_girl.IsVisible && _girl.Exposure == 0,
                "A new run clears commitment, gaze and the rare-day selection");
            _session.Running = false;
            Done = true; Passed = _checks.All(c => c.StartsWith("PASS "));
            Stage = "Finished";
            File.WriteAllText(_output + "/playmode.json", JsonUtility.ToJson(new Report { Done = Done, Passed = Passed, Checks = _checks.ToArray() }, true));
            RestoreRecord();
        }

        private void ResetPhotos()
        {
            _session.BeginRun();
            _session.Running = false; _session.Preparation = 0;
            _player.InputEnabled = false; _player.enabled = false; _player.LockCursor(false);
        }

        private void FreezeSea()
        {
            _session.Observations.Automatic = false; _session.Observations.Clear();
            _director.FreezePreview = true; _director.Encounter.FrozenPreview = true;
            _director.BoatVisible = _director.KrakenVisible = _director.ShoalVisible = _director.Feast = false;
            _director.Boat.gameObject.SetActive(false); _director.Kraken.gameObject.SetActive(false); _director.Shoal.gameObject.SetActive(false);
            _director.Traffic.Frozen = true;
            foreach (var ship in _director.Traffic.Ships)
            {
                ship.gameObject.SetActive(false);
                if (ship.TryGetComponent<OceanBuoyancy>(out var buoyancy)) buoyancy.enabled = false;
            }
            if (CinematicEnvironment.Active.Bolt) CinematicEnvironment.Active.Bolt.Clear();
        }

        private void Aim(Vector3 cameraPosition, Vector3 target)
        {
            _player.Teleport(cameraPosition - Vector3.up * 1.65f, target);
            _player.View.transform.position = cameraPosition;
            _player.View.transform.rotation = Quaternion.LookRotation(target - cameraPosition);
            Physics.SyncTransforms();
        }

        private void AimGirl(bool towards)
        {
            Vector3 position = _girl.transform.TransformPoint(new Vector3(0, .13f, 3.1f)) + Vector3.up * 1.65f;
            Aim(position, towards ? _girl.SeatedFocus : position + Vector3.right * 20);
        }

        private WeepingGirlAppearance Appearance => (WeepingGirlAppearance)typeof(WeepingGirlEncounter)
            .GetField("_appearance", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(_girl);

        private void SelectDay(int day, float roll)
        {
            _session.Day = day;
            Appearance.BeginDay(day, roll);
            _girl.ResetEncounter();
        }

        private GameObject Barrier(Vector3 from, Vector3 to, float size)
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Photo reward probe occluder";
            wall.transform.SetPositionAndRotation(Vector3.Lerp(from, to, .5f), Quaternion.LookRotation(to - from));
            wall.transform.localScale = new Vector3(size, size, .2f);
            Physics.SyncTransforms();
            return wall;
        }

        private IEnumerator Capture(string file)
        {
            yield return null;
            _session.BusyUntil = 0; _session.Capture();
            yield return new WaitForEndOfFrame(); yield return null;
            if (file != null && _session.Shots.Count > 0)
                File.WriteAllBytes(_output + "/" + file + ".png", _session.Shots.Last().Image.EncodeToPNG());
        }

        private void RestoreRecord()
        {
            if (_hadRecord) PlayerPrefs.SetInt("Lighthouse.Blockout.Best", _record);
            else PlayerPrefs.DeleteKey("Lighthouse.Blockout.Best");
            PlayerPrefs.Save();
        }

        private void OnDestroy() { RestoreRecord(); }
    }
}
#endif
