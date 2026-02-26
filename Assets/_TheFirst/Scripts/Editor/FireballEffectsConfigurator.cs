using UnityEngine;
using UnityEditor;
using System.IO;

public static class FireballEffectsConfigurator
{
    [MenuItem("Tools/Configure Fireball Effects")]
    public static void Configure()
    {
        // 1. 找到 ExplosiveFireball.prefab
        string fireballPrefabPath = "Assets/_TheFirst/Prefabs/Gameplay/ExplosiveFireball.prefab";
        GameObject fireballPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(fireballPrefabPath);
        
        if (fireballPrefab == null)
        {
            Debug.LogError($"未找到 Fireball Prefab: {fireballPrefabPath}");
            return;
        }

        // 2. 复制创建 Spark Prefab
        string sparkPrefabPath = "Assets/_TheFirst/Prefabs/Gameplay/ExplosiveFireball_Spark.prefab";
        
        // 如果文件已存在，先删除（或者不操作）
        if (!File.Exists(sparkPrefabPath))
        {
             if(AssetDatabase.CopyAsset(fireballPrefabPath, sparkPrefabPath))
             {
                 Debug.Log("Spark Prefab 复制成功");
             }
             else
             {
                 Debug.LogError("Spark Prefab 复制失败");
                 return;
             }
             AssetDatabase.Refresh();
        }
        
        GameObject sparkPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(sparkPrefabPath);
        if (sparkPrefab == null)
        {
            Debug.LogError("Spark Prefab 创建/加载失败");
            return;
        }

        // 3. 修改 Spark Prefab 属性
        // 使用 PrefabUtility.LoadPrefabContents
        GameObject sparkContent = PrefabUtility.LoadPrefabContents(sparkPrefabPath);
        
        sparkContent.transform.localScale = Vector3.one * 0.5f; // 缩小体积
        
        // 确保 Spark 不会再分裂 (双重保险，虽然代码里也有 canSplit)
        // Projectile 脚本本身没有 subProjectile 这种字段，那是 WeaponStatBlock 的数据。
        // 但我们可以修改 Projectile 上的一些参数如果需要的话。
        // 主要是要确保它的名字改一下，方便调试
        sparkContent.name = "ExplosiveFireball_Spark";
        
        PrefabUtility.SaveAsPrefabAsset(sparkContent, sparkPrefabPath);
        PrefabUtility.UnloadPrefabContents(sparkContent);
        
        // 4. 配置 SO_Fireball
        string soPath = "Assets/_TheFirst/GameData/SO_Weapon/SO_Fireball.asset";
        WeaponStatBlock so = AssetDatabase.LoadAssetAtPath<WeaponStatBlock>(soPath);
        if (so != null)
        {
            so.subProjectilePrefab = sparkPrefab;
            so.subProjectileCount = 0; // 默认 0，靠技能树加成
            
            EditorUtility.SetDirty(so);
            AssetDatabase.SaveAssets();
            Debug.Log($"成功配置 SO_Fireball: SubPrefab={sparkPrefab.name}, Count=0");
        }
        else
        {
             Debug.LogError($"未找到 SO_Fireball: {soPath}");
        }
    }
}
