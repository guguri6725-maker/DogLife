using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
namespace DogLife.EditorTools
{
    public static class SpringBuildOptimizer
    {
        const string Backup="Builds/SpringOptimizationBackup/";
        public static string Apply()
        {
            var report=UnityEditor.Build.Reporting.BuildReport.GetLatestReport();
            var paths=report.packedAssets.SelectMany(p=>p.contents).Select(p=>p.sourceAssetPath).Where(p=>p.StartsWith("Assets/")).Distinct().ToArray();
            int models=0,meshes=0,textures=0;
            foreach(var path in paths)
            {
                var importer=AssetImporter.GetAtPath(path);
                var model=importer as ModelImporter;
                if(model!=null)
                {
                    BackupFile(path+".meta");
                    model.meshCompression=path.Contains("/Spring/")?ModelImporterMeshCompression.Medium:ModelImporterMeshCompression.Low;
                    model.SaveAndReimport();models++;continue;
                }
                var mesh=AssetDatabase.LoadMainAssetAtPath(path) as Mesh;
                if(mesh!=null&&path.StartsWith("Assets/MapDesigne/Spring/LOD/"))
                {
                    BackupFile(path);MeshUtility.SetMeshCompression(mesh,ModelImporterMeshCompression.Medium);EditorUtility.SetDirty(mesh);meshes++;continue;
                }
                var texture=importer as TextureImporter;
                if(texture!=null&&texture.textureType!=TextureImporterType.Sprite&&!path.Contains("/UI/"))
                {
                    BackupFile(path+".meta");
                    var platform=texture.GetPlatformTextureSettings("Standalone");
                    platform.overridden=true;platform.maxTextureSize=texture.textureType==TextureImporterType.NormalMap?1024:2048;
                    platform.format=TextureImporterFormat.Automatic;platform.textureCompression=TextureImporterCompression.CompressedHQ;
                    platform.crunchedCompression=true;platform.compressionQuality=85;
                    texture.SetPlatformTextureSettings(platform);texture.SaveAndReimport();textures++;
                }
            }
            AssetDatabase.SaveAssets();return "models="+models+", meshes="+meshes+", textures="+textures;
        }
        static void BackupFile(string path)
        {
            string dest=Backup+path;if(File.Exists(dest))return;
            Directory.CreateDirectory(Path.GetDirectoryName(dest));File.Copy(path,dest);
        }
    }
}


