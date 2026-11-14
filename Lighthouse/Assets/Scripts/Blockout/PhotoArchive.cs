using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace LighthouseGame
{
    [Serializable]
    public sealed class ArchivedPhoto
    {
        public string Id, Label;
        public int Price;
        public long Captured;
        public List<PhotoSubject> Subjects = new List<PhotoSubject>();
    }

    [Serializable]
    public sealed class PhotoArchiveData
    {
        public int Version = 1, Best;
        public List<ArchivedPhoto> Photos = new List<ArchivedPhoto>();
    }

    // Owns copies of completed captures, never the live five-slot inventory textures.
    public sealed class PhotoArchive
    {
        public const int Capacity = 20;
        private readonly string _directory, _manifest;
        private bool _readOnly;
        public PhotoArchiveData Data { get; private set; } = new PhotoArchiveData();
        public string Error { get; private set; }

        public PhotoArchive(string directory, int legacyBest)
        {
            _directory = directory; _manifest = Path.Combine(directory, "archive.json");
            try
            {
                if (File.Exists(_manifest)) Data = Read(_manifest);
            }
            catch (Exception exception)
            {
                _readOnly = exception is NotSupportedException;
                Error = "Архив повреждён. Исходный файл сохранён.";
                try
                {
                    if (File.Exists(_manifest + ".bak")) Data = Read(_manifest + ".bak");
                    File.Copy(_manifest, _manifest + ".corrupt-" + DateTime.UtcNow.Ticks);
                }
                catch { _readOnly = true; }
                Debug.LogWarning(Error + " " + exception.Message);
            }
            Data.Best = Math.Max(legacyBest, Math.Max(0, Data.Best));
            Data.Photos = Ordered(Data.Photos);
        }

        private static PhotoArchiveData Read(string path)
        {
            var data = JsonUtility.FromJson<PhotoArchiveData>(File.ReadAllText(path));
            if (data != null && data.Version > 1) throw new NotSupportedException("Newer photo archive format.");
            if (data == null || data.Version != 1 || data.Photos == null) throw new InvalidDataException("Unknown photo archive format.");
            return data;
        }

        public static List<ArchivedPhoto> Ordered(IEnumerable<ArchivedPhoto> photos)
        {
            return (photos ?? Enumerable.Empty<ArchivedPhoto>())
                .Where(photo => photo != null && photo.Price > 0 && Guid.TryParseExact(photo.Id, "N", out _))
                .GroupBy(photo => photo.Id).Select(group => group.First())
                .OrderByDescending(photo => photo.Price).ThenBy(photo => photo.Captured).ThenBy(photo => photo.Id)
                .Take(Capacity).ToList();
        }

        public bool Record(BlockoutShot shot, int score, bool isRun)
        {
            if (!isRun || shot == null || !shot.Image) return false;
            Data.Best = Math.Max(Data.Best, score);
            if (shot.Price <= 0 || (Data.Photos.Count >= Capacity && shot.Price <= Data.Photos[Data.Photos.Count - 1].Price)) return Save();
            var photo = new ArchivedPhoto { Id = Guid.NewGuid().ToString("N"), Label = shot.Label, Price = shot.Price,
                Captured = DateTime.UtcNow.Ticks, Subjects = shot.Subjects.Select(s => new PhotoSubject { Key = s.Key, Label = s.Label, Price = s.Price }).ToList() };
            if (_readOnly) return false;
            try
            {
                Directory.CreateDirectory(_directory);
                var path = ImagePath(photo);
                File.WriteAllBytes(path + ".tmp", shot.Image.EncodeToJPG(92));
                File.Move(path + ".tmp", path);
                var previous = Data.Photos;
                Data.Photos = Ordered(previous.Concat(new[] { photo }));
                if (!Save()) { Data.Photos = previous; return false; }
                PruneImages(); return true;
            }
            catch (Exception exception) { Error = "Снимок не сохранён: " + exception.Message; return false; }
        }

        public void RecordBest(int score) { if (score > Data.Best) { Data.Best = score; Save(); } }

        private bool Save()
        {
            if (_readOnly) return false;
            try { MenuFiles.WriteAtomic(_manifest, JsonUtility.ToJson(Data, true)); Error = null; return true; }
            catch (Exception exception) { Error = "Архив не сохранён: " + exception.Message; return false; }
        }

        private string ImagePath(ArchivedPhoto photo)
        {
            if (!Guid.TryParseExact(photo.Id, "N", out _)) throw new InvalidDataException("Invalid photo id.");
            return Path.Combine(_directory, photo.Id + ".jpg");
        }

        public Texture2D LoadImage(ArchivedPhoto photo)
        {
            Texture2D texture = null;
            try
            {
                var path = ImagePath(photo);
                if (!File.Exists(path)) return null;
                texture = new Texture2D(2, 2, TextureFormat.RGB24, false);
                if (texture.LoadImage(File.ReadAllBytes(path))) return texture;
            }
            catch (Exception exception) { Debug.LogWarning("Archive image unavailable: " + exception.Message); }
            if (texture) UnityEngine.Object.Destroy(texture);
            return null;
        }

        private void PruneImages()
        {
            // Keep the previous manifest's images as well, so the backup remains useful.
            var retained = new HashSet<string>(Data.Photos.Select(photo => photo.Id));
            if (File.Exists(_manifest + ".bak")) foreach (var photo in Ordered(Read(_manifest + ".bak").Photos)) retained.Add(photo.Id);
            foreach (var file in Directory.EnumerateFiles(_directory, "*.jpg"))
            {
                var id = Path.GetFileNameWithoutExtension(file);
                if (Guid.TryParseExact(id, "N", out _) && !retained.Contains(id)) File.Delete(file);
            }
        }
    }
}
