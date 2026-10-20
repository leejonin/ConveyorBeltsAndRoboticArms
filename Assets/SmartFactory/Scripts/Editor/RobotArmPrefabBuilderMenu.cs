using System.IO;
using SmartFactory.Robot;
using UnityEditor;
using UnityEngine;

namespace SmartFactory.Editor
{
    /// <summary>
    /// Robotic-Arm-Rig.fbx 기반 RobotArm_Rig 프리팹과 바인딩 에셋을 생성합니다.
    /// </summary>
    public static class RobotArmPrefabBuilderMenu
    {
        const string FbxPath = "Assets/RobotArm/Robotic-Arm-Rig.fbx";
        const string PrefabDir = "Assets/RobotArm/Prefabs";
        const string PrefabPath = PrefabDir + "/RobotArm_Rig.prefab";
        const string BindingPath = "Assets/RobotArm/RoboticArmRigBinding.asset";
        const string PoseSetPath = "Assets/RobotArm/RobotArmDefaultPoseSet.asset";
        const string ResourcesDir = "Assets/Resources/SmartFactory";
        const string ResourcesPrefabPath = ResourcesDir + "/RobotArm_Rig.prefab";

        [MenuItem("SmartFactory/Create Robot Arm Rig Prefab")]
        static void CreatePrefab()
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
            if (fbx == null)
            {
                EditorUtility.DisplayDialog("SmartFactory", $"FBX를 찾을 수 없습니다: {FbxPath}", "확인");
                return;
            }

            Directory.CreateDirectory(PrefabDir);
            Directory.CreateDirectory(ResourcesDir);

            var binding = AssetDatabase.LoadAssetAtPath<RobotArmRigBinding>(BindingPath);
            if (binding == null)
            {
                binding = ScriptableObject.CreateInstance<RobotArmRigBinding>();
                AssetDatabase.CreateAsset(binding, BindingPath);
            }

            var poseSet = AssetDatabase.LoadAssetAtPath<RobotArmPoseSet>(PoseSetPath);

            var root = new GameObject("RobotArm_Rig");
            var model = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
            model.name = "Robotic-Arm-Rig";
            model.transform.SetParent(root.transform, false);

            var controller = root.AddComponent<RobotArmController>();
            var so = new SerializedObject(controller);
            so.FindProperty("modelRoot").objectReferenceValue = model.transform;
            so.FindProperty("rigBinding").objectReferenceValue = binding;
            so.FindProperty("poseSet").objectReferenceValue = poseSet;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            PrefabUtility.SaveAsPrefabAsset(root, ResourcesPrefabPath);
            Object.DestroyImmediate(root);

            AutoFillBindingFromModel(binding, AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
            EditorUtility.SetDirty(binding);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog(
                "SmartFactory",
                $"프리팹 생성 완료:\n{PrefabPath}\n\nFactoryConfig의 robotArmPrefab에 연결하거나 Resources/SmartFactory/RobotArm_Rig 를 사용하세요.\n바인딩: {BindingPath}",
                "확인");
        }

        static void AutoFillBindingFromModel(RobotArmRigBinding binding, GameObject prefab)
        {
            if (prefab == null || binding == null)
            {
                return;
            }

            binding.jointPaths = System.Array.Empty<string>();
            binding.visualRootPath = string.Empty;
            binding.principalPath = "Principal";
            binding.brazo1Path = "Brazo1";
            binding.brazo2Path = "Brazo2";
            binding.brazo3Path = "Brazo3";
            binding.gancho1Path = "Gancho1";
            binding.gancho2Path = "Gancho2";
            binding.endEffectorPath = "Piston_end";
        }
    }
}
