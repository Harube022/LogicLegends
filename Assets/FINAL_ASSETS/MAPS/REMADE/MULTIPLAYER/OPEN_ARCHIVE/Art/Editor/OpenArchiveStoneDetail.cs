using UnityEditor;
using UnityEngine;

namespace OpenArchiveAuthoring
{
    public static class OpenArchiveStoneDetail
    {
        [MenuItem("Tools/Logic Legends/Open Archive/Art/Apply visible stone textures")]
        public static void Apply()
        {
            string map=OpenArchiveBuilder.Folder,art=map+"/Art",path=art+"/Textures/Archive_StoneGrain.png";
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            importer.sRGBTexture=false;importer.mipmapEnabled=true;importer.wrapMode=TextureWrapMode.Repeat;
            importer.filterMode=FilterMode.Trilinear;importer.anisoLevel=4;importer.maxTextureSize=512;importer.isReadable=false;
            var android=importer.GetPlatformTextureSettings("Android");android.overridden=true;android.maxTextureSize=512;android.format=TextureImporterFormat.ASTC_6x6;
            importer.SetPlatformTextureSettings(android);importer.SaveAndReimport();
            var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            var paths=new[]{map+"/Materials/Archive_Atlas.mat",art+"/Materials/Archive_WeatheredStone_Vertex.mat",art+"/Materials/Archive_MossAndStone.mat",art+"/Materials/Archive_LayeredCliff.mat",art+"/Materials/Archive_WarmStonePaving.mat",art+"/Materials/Archive_WarmStonePaving_Vertex.mat"};
            foreach(string materialPath in paths){
                var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                material.SetTexture("_StoneMap",texture);material.SetFloat("_StoneTextureStrength",material.GetFloat("_SurfaceStyle")>5.5f?.64f:.82f);
                material.SetFloat("_DetailStrength",.92f);EditorUtility.SetDirty(material);
            }
            AssetDatabase.SaveAssets();
        }
    }
}
