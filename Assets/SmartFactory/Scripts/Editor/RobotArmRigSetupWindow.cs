using System.Collections.Generic;
using System.Linq;
using SmartFactory.Robot;
using UnityEditor;
using UnityEngine;

namespace SmartFactory.Editor
{
    /// <summary>
    /// FBX 인스턴스에서 RobotArmRigBinding 본 경로를 설정합니다.
    /// </summary>
    public class RobotArmRigSetupWindow : EditorWindow
    {
        RobotArmRigBinding m_binding;
        GameObject m_selectedRoot;
        Vector2 m_scroll;
        readonly List<Transform> m_allTransforms = new List<Transform>();

        static readonly string[] RequiredMotionNames =
        {
            "Principal", "Brazo1", "Brazo2", "Brazo3", "Gancho1", "Gancho2", "Piston_end"
        };

        [MenuItem("SmartFactory/Robot Arm Rig Setup")]
        static void Open()
        {
            GetWindow<RobotArmRigSetupWindow>("Robot Arm Rig");
        }

        void OnGUI()
        {
            EditorGUILayout.LabelField("Robotic-Arm-Rig 바인딩 (로봇팔_움직임.md)", EditorStyles.boldLabel);
            m_binding = (RobotArmRigBinding)EditorGUILayout.ObjectField("Binding Asset", m_binding, typeof(RobotArmRigBinding), false);
            m_selectedRoot = (GameObject)EditorGUILayout.ObjectField("Model Root (FBX Instance)", m_selectedRoot, typeof(GameObject), true);

            if (GUILayout.Button("Refresh Hierarchy"))
            {
                RefreshTransforms();
            }

            if (m_binding == null)
            {
                return;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("회전 제어 경로", EditorStyles.boldLabel);
            m_binding.principalPath = EditorGUILayout.TextField("Principal", m_binding.principalPath);
            m_binding.brazo1Path = EditorGUILayout.TextField("Brazo1", m_binding.brazo1Path);
            m_binding.brazo2Path = EditorGUILayout.TextField("Brazo2", m_binding.brazo2Path);
            m_binding.brazo3Path = EditorGUILayout.TextField("Brazo3", m_binding.brazo3Path);
            m_binding.gancho1Path = EditorGUILayout.TextField("Gancho1", m_binding.gancho1Path);
            m_binding.gancho2Path = EditorGUILayout.TextField("Gancho2", m_binding.gancho2Path);
            m_binding.endEffectorPath = EditorGUILayout.TextField("End Effector (Joint anchor)", m_binding.endEffectorPath);

            if (GUILayout.Button("Apply Document Default Names"))
            {
                ApplyDocumentDefaults();
            }

            if (GUILayout.Button("Validate Motion Paths"))
            {
                ValidateMotionPaths();
            }

            if (m_selectedRoot == null)
            {
                return;
            }

            m_scroll = EditorGUILayout.BeginScrollView(m_scroll, GUILayout.Height(180));
            foreach (var t in m_allTransforms)
            {
                EditorGUILayout.LabelField(t.name);
            }

            EditorGUILayout.EndScrollView();

            if (GUILayout.Button("Save Binding Asset"))
            {
                EditorUtility.SetDirty(m_binding);
                AssetDatabase.SaveAssets();
                EditorUtility.DisplayDialog("SmartFactory", "RobotArmRigBinding 저장 완료", "확인");
            }
        }

        void ApplyDocumentDefaults()
        {
            m_binding.principalPath = "Principal";
            m_binding.brazo1Path = "Brazo1";
            m_binding.brazo2Path = "Brazo2";
            m_binding.brazo3Path = "Brazo3";
            m_binding.gancho1Path = "Gancho1";
            m_binding.gancho2Path = "Gancho2";
            m_binding.endEffectorPath = "Piston_end";
            m_binding.jointPaths = System.Array.Empty<string>();
        }

        void ValidateMotionPaths()
        {
            RefreshTransforms();
            if (m_selectedRoot == null || m_binding == null)
            {
                EditorUtility.DisplayDialog("SmartFactory", "Model Root를 지정하세요.", "확인");
                return;
            }

            if (!m_binding.TryResolveMotionRig(m_selectedRoot.transform, out var rig))
            {
                EditorUtility.DisplayDialog("SmartFactory", "TryResolveMotionRig 실패. 경로·계층을 확인하세요.", "확인");
                return;
            }

            var names = m_allTransforms.Select(t => t.name).ToList();
            var missing = RequiredMotionNames.Where(n => !names.Contains(n)).ToList();
            var duplicates = names.GroupBy(n => n).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            var forbidden = new List<string>();
            if (names.Contains("Arm Rig"))
            {
                forbidden.Add("Arm Rig (회전 금지)");
            }

            var msg = "Motion rig 해석 성공.\n" +
                      $"Principal={rig.Principal.name}, End={rig.EndEffector.name}\n";
            if (missing.Count > 0)
            {
                msg += "\n이름 미발견(계층 내): " + string.Join(", ", missing);
            }

            if (duplicates.Count > 0)
            {
                msg += "\n중복 이름: " + string.Join(", ", duplicates);
            }

            if (forbidden.Count > 0)
            {
                msg += "\n주의: " + string.Join(", ", forbidden);
            }

            EditorUtility.DisplayDialog("SmartFactory", msg, "확인");
        }

        void RefreshTransforms()
        {
            m_allTransforms.Clear();
            if (m_selectedRoot == null)
            {
                return;
            }

            CollectTransforms(m_selectedRoot.transform, m_allTransforms);
        }

        static void CollectTransforms(Transform root, List<Transform> output)
        {
            output.Add(root);
            for (var i = 0; i < root.childCount; i++)
            {
                CollectTransforms(root.GetChild(i), output);
            }
        }
    }
}
