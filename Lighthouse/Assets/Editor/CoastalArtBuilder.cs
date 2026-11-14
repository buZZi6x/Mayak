using System.IO;
using System.Linq;
using LighthouseGame;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

public static class CoastalArtBuilder
{
    public const string Root = "Assets/Art/Coastal";
    public static string ModelPath(string name)
    {
        string path = Root + "/Models/" + name + ".fbx";
        return File.Exists(path) ? path : "Assets/Art/Blockout/" + name + ".fbx";
    }
    public static Material FindMaterial(string name) => AssetDatabase.LoadAssetAtPath<Material>(Root + "/Materials/" + name + ".mat");
    public static Mesh CollisionMesh(Mesh source)
    {
        // Keep cooked physics independent of the imported FBX's cached mesh data.
        // Reimporting a changed FBX can otherwise leave an enabled MeshCollider with
        // zero bounds in an already running Editor, despite a valid rendered mesh.
        Directory.CreateDirectory(Root+"/Collisions");
        string name=string.Concat(source.name.Select(c=>Path.GetInvalidFileNameChars().Contains(c)?'_':c));
        string path=Root+"/Collisions/"+name+".asset";
        var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(!mesh){mesh=new Mesh{name="Collision · "+source.name};AssetDatabase.CreateAsset(mesh,path);}
        mesh.Clear();mesh.indexFormat=source.indexFormat;mesh.vertices=source.vertices;mesh.triangles=source.triangles;mesh.RecalculateBounds();
        EditorUtility.SetDirty(mesh);return mesh;
    }
    public static void PrepareMaterials()
    {
        if (!Directory.Exists(Root + "/Textures")) return;
        Directory.CreateDirectory(Root + "/Materials");
        foreach (string path in Directory.GetFiles(Root + "/Textures", "*.png").Select(p=>p.Replace('\\','/')))
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            bool normal = path.Contains("_Normal"), color = path.Contains("_Base");
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = color; importer.maxTextureSize = 1024;
            importer.mipmapEnabled = true; importer.anisoLevel = 8; importer.wrapMode = TextureWrapMode.Repeat;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
        }
        foreach (string path in Directory.GetFiles(Root + "/Textures", "*_Base.png").Select(p=>p.Replace('\\','/')))
        {
            string name = Path.GetFileNameWithoutExtension(path).Replace("_Base", "");
            var m = FindMaterial(name);
            if (!m) { m = new Material(Shader.Find("HDRP/Lit")); AssetDatabase.CreateAsset(m, Root + "/Materials/" + name + ".mat"); }
            m.shader = Shader.Find("HDRP/Lit"); m.SetColor("_BaseColor", Color.white);
            m.SetTexture("_BaseColorMap", AssetDatabase.LoadAssetAtPath<Texture2D>(path));
            m.SetTexture("_NormalMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Textures/" + name + "_Normal.png"));
            m.SetTexture("_MaskMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Textures/" + name + "_Mask.png"));
            m.SetFloat("_NormalScale", name == "Coast_Rock" ? 1.0f : name=="Coast_Linen"||name=="Coast_Cloth"?.25f:.55f);
            m.SetFloat("_Metallic", 1); m.SetFloat("_Smoothness", .5f);
            m.SetFloat("_SmoothnessRemapMin", 0); m.SetFloat("_SmoothnessRemapMax", 1);
            HDMaterial.ValidateMaterial(m); EditorUtility.SetDirty(m);
        }
        AssetDatabase.SaveAssets();
    }
    public static Material[] ExteriorMaterials() => new[] {"Coast_Rock", "Coast_Masonry", "Coast_Copper", "Coast_Red"}.Select(FindMaterial).Where(m => m).ToArray();
}
