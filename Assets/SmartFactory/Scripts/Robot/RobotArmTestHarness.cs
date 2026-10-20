using System;
using UnityEngine;

namespace SmartFactory.Robot
{
    /// <summary>
    /// 로봇팔 테스트용 OnGUI 창: 포즈 프리셋 + 관절 슬라이더(실시간 회전).
    /// </summary>
    public class RobotArmTestHarness : MonoBehaviour
    {
        const float PanelWidth = 300f;
        const float PanelMaxHeight = 460f;

        [SerializeField] RobotArmController targetArm;
        [SerializeField] bool showPanel = true;
        [SerializeField] KeyCode togglePanelKey = KeyCode.F1;

        readonly float[] m_jointUi = new float[7];
        string[] m_poseNamesRuntime = Array.Empty<string>();
        int m_selectedPoseIndex;
        float m_blendDuration = 1.2f;
        Vector2 m_scroll;
        Rect m_panelRect = new Rect(12f, 12f, PanelWidth, PanelMaxHeight);
        bool m_syncJointUiNextFrame;

        static readonly RobotArmDof[] s_dofs =
        {
            RobotArmDof.PrincipalY,
            RobotArmDof.PrincipalZ,
            RobotArmDof.Brazo1Z,
            RobotArmDof.Brazo2Z,
            RobotArmDof.Brazo3Z,
            RobotArmDof.Gancho1Z,
            RobotArmDof.Gancho2Z
        };

        void Awake()
        {
            DestroyLegacyJointBarCanvas();
        }

        void Start()
        {
            if (targetArm == null)
            {
                targetArm = FindFirstObjectByType<RobotArmController>();
            }

            DestroyLegacyJointBarCanvas();
            ClampPanelToScreen();
            RefreshPoseList();
            SyncJointUiFromArm();
        }

        void Update()
        {
            if (Input.GetKeyDown(togglePanelKey))
            {
                showPanel = !showPanel;
            }

            if (m_syncJointUiNextFrame)
            {
                m_syncJointUiNextFrame = false;
                SyncJointUiFromArm();
            }
        }

        void OnGUI()
        {
            if (!showPanel || targetArm == null)
            {
                return;
            }

            ClampPanelToScreen();
            m_panelRect = GUI.Window(91024, m_panelRect, DrawPanelWindow, "로봇팔 테스트 제어");
        }

        void DrawPanelWindow(int id)
        {
            var contentHeight = PanelMaxHeight - 28f;
            m_scroll = GUILayout.BeginScrollView(m_scroll, GUILayout.Width(m_panelRect.width - 8f), GUILayout.Height(contentHeight));

            if (!targetArm.IsMotionReady)
            {
                GUILayout.Label("드라이버 미준비\nPrincipal/Brazo/Gancho 바인딩 확인");
                GUILayout.EndScrollView();
                GUI.DragWindow(new Rect(0f, 0f, m_panelRect.width, 22f));
                return;
            }

            if (Event.current.type == EventType.Repaint && GUIUtility.hotControl == 0 && !targetArm.IsBusy)
            {
                SyncJointUiFromArm();
            }

            GUILayout.Label($"{targetArm.name} · {targetArm.CurrentTask}", BoldLabel());
            if (targetArm.EndEffector != null)
            {
                var w = targetArm.EndEffector.position;
                GUILayout.Label($"Piston_end ({w.x:F2}, {w.y:F2}, {w.z:F2})", SmallLabel());
            }

            GUILayout.Space(4f);
            DrawPoseSection();
            GUILayout.Space(6f);
            DrawJointBarSection();

            GUILayout.Space(4f);
            GUILayout.Label($"{togglePanelKey}: 패널 표시/숨김 · 창 드래그: 상단 제목줄", SmallLabel());
            GUILayout.EndScrollView();
            GUI.DragWindow(new Rect(0f, 0f, m_panelRect.width, 22f));
        }

        void DrawPoseSection()
        {
            GUILayout.Label("포즈 프리셋", BoldLabel());
            m_blendDuration = SliderRow("보간(s)", m_blendDuration, 0.2f, 4f);
            targetArm.PoseBlendDuration = m_blendDuration;

            if (m_poseNamesRuntime.Length > 0)
            {
                m_selectedPoseIndex = GUILayout.SelectionGrid(
                    m_selectedPoseIndex,
                    m_poseNamesRuntime,
                    1,
                    GUILayout.MaxWidth(PanelWidth - 24f));
            }

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("즉시") && m_poseNamesRuntime.Length > 0)
            {
                ApplyNamedPoseInstant(m_poseNamesRuntime[m_selectedPoseIndex]);
            }

            if (GUILayout.Button("보간") && m_poseNamesRuntime.Length > 0 && !targetArm.IsBusy)
            {
                targetArm.BlendToNamedPose(m_poseNamesRuntime[m_selectedPoseIndex]);
            }

            if (GUILayout.Button("Idle"))
            {
                ApplyNamedPoseInstant("Idle");
            }

            GUILayout.EndHorizontal();

            if (GUILayout.Button("분류 포즈 시퀀스") && !targetArm.IsBusy)
            {
                targetArm.PlayPoseSequenceDemo();
            }
        }

        void DrawJointBarSection()
        {
            GUILayout.Label("관절 직접 제어", BoldLabel());
            for (var i = 0; i < s_dofs.Length; i++)
            {
                DrawJointBar(s_dofs[i], i);
            }

            if (GUILayout.Button("슬라이더 ← 현재 자세"))
            {
                SyncJointUiFromArm();
            }
        }

        void DrawJointBar(RobotArmDof dof, int index)
        {
            if (!targetArm.TryGetJointLimits(dof, out var min, out var max))
            {
                return;
            }

            GUILayout.BeginHorizontal(GUILayout.MaxWidth(PanelWidth - 24f));
            GUILayout.Label(DofShortLabel(dof), GUILayout.Width(72f));
            var newVal = GUILayout.HorizontalSlider(m_jointUi[index], min, max, GUILayout.MinWidth(100f));
            GUILayout.Label($"{newVal:F0}°", GUILayout.Width(34f));
            GUILayout.EndHorizontal();

            if (Math.Abs(newVal - m_jointUi[index]) > 0.01f)
            {
                m_jointUi[index] = newVal;
                targetArm.SetJointAngle(dof, newVal);
            }

            GUILayout.Label($"    {min:F0}° ~ {max:F0}°", SmallLabel());
        }

        static float SliderRow(string label, float value, float min, float max)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(72f));
            var v = GUILayout.HorizontalSlider(value, min, max);
            GUILayout.Label($"{v:F1}", GUILayout.Width(34f));
            GUILayout.EndHorizontal();
            return v;
        }

        void ApplyNamedPoseInstant(string poseName)
        {
            if (targetArm.PoseSet == null || !targetArm.PoseSet.TryGetPose(poseName, out var pose))
            {
                return;
            }

            targetArm.ApplyManualPose(pose);
            m_syncJointUiNextFrame = true;
        }

        void SyncJointUiFromArm()
        {
            if (targetArm == null || !targetArm.IsMotionReady)
            {
                return;
            }

            for (var i = 0; i < s_dofs.Length; i++)
            {
                m_jointUi[i] = targetArm.GetJointAngle(s_dofs[i]);
            }
        }

        void RefreshPoseList()
        {
            if (targetArm?.PoseSet == null)
            {
                m_poseNamesRuntime = Array.Empty<string>();
                return;
            }

            targetArm.PoseSet.EnsureDefaultPoses();
            var poses = targetArm.PoseSet.poses;
            if (poses == null || poses.Length == 0)
            {
                m_poseNamesRuntime = Array.Empty<string>();
                return;
            }

            m_poseNamesRuntime = new string[poses.Length];
            for (var i = 0; i < poses.Length; i++)
            {
                m_poseNamesRuntime[i] = poses[i] != null ? poses[i].poseName : $"Pose_{i}";
            }
        }

        void ClampPanelToScreen()
        {
            m_panelRect.width = PanelWidth;
            m_panelRect.height = Mathf.Min(PanelMaxHeight, Screen.height - 24f);
            m_panelRect.x = Mathf.Clamp(m_panelRect.x, 0f, Mathf.Max(0f, Screen.width - m_panelRect.width));
            m_panelRect.y = Mathf.Clamp(m_panelRect.y, 0f, Mathf.Max(0f, Screen.height - m_panelRect.height));
        }

        static string DofShortLabel(RobotArmDof dof)
        {
            switch (dof)
            {
                case RobotArmDof.PrincipalY: return "Prin Y";
                case RobotArmDof.PrincipalZ: return "Prin Z";
                case RobotArmDof.Brazo1Z: return "Brazo1";
                case RobotArmDof.Brazo2Z: return "Brazo2";
                case RobotArmDof.Brazo3Z: return "Brazo3";
                case RobotArmDof.Gancho1Z: return "Ganch1";
                case RobotArmDof.Gancho2Z: return "Ganch2";
                default: return dof.ToString();
            }
        }

        static GUIStyle BoldLabel()
        {
            return new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
        }

        static GUIStyle SmallLabel()
        {
            return new GUIStyle(GUI.skin.label) { fontSize = 11 };
        }

        static void DestroyLegacyJointBarCanvas()
        {
            var canvas = GameObject.Find("RobotArmJointBarCanvas");
            if (canvas != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(canvas);
                }
                else
                {
                    DestroyImmediate(canvas);
                }
            }

            var eventSystem = GameObject.Find("EventSystem");
            if (eventSystem != null && UnityEngine.Object.FindFirstObjectByType<Canvas>() == null)
            {
                if (Application.isPlaying)
                {
                    Destroy(eventSystem);
                }
                else
                {
                    DestroyImmediate(eventSystem);
                }
            }
        }

        /// <summary>
        /// 테스트 대상 로봇팔을 지정합니다.
        /// </summary>
        public void SetTarget(RobotArmController arm)
        {
            targetArm = arm;
            RefreshPoseList();
            SyncJointUiFromArm();
        }
    }
}
