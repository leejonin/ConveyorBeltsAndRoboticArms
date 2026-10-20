using System;
using UnityEngine;

namespace SmartFactory.Robot
{
    /// <summary>
    /// 단일 포즈: 문서 DOF별 로컬 회전 각(도).
    /// </summary>
    [Serializable]
    public class RobotArmPose
    {
        public string poseName = "Idle";
        public float principalY;
        public float principalZ;
        public float brazo1Z;
        public float brazo2Z;
        public float brazo3Z;
        public float gancho1Z = -15f;
        public float gancho2Z = 15f;
    }

    /// <summary>
    /// 분류 시퀀스용 관절 각도 포즈 모음.
    /// </summary>
    [CreateAssetMenu(fileName = "RobotArmPoseSet", menuName = "SmartFactory/Robot Arm Pose Set")]
    public class RobotArmPoseSet : ScriptableObject
    {
        public RobotArmPose[] poses = Array.Empty<RobotArmPose>();

        /// <summary>
        /// 이름으로 포즈를 찾습니다.
        /// </summary>
        public bool TryGetPose(string poseName, out RobotArmPose pose)
        {
            pose = null;
            if (string.IsNullOrEmpty(poseName) || poses == null)
            {
                return false;
            }

            foreach (var p in poses)
            {
                if (p != null && p.poseName == poseName)
                {
                    pose = p;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 기본 포즈 세트를 채웁니다(에디터·최초 생성용).
        /// </summary>
        public void EnsureDefaultPoses()
        {
            if (TryGetPose("LiftHold", out _) && TryGetPose("ReachPick", out _) && TryGetPose("GripClosedAtReach", out _))
            {
                return;
            }

            if (poses == null || poses.Length == 0)
            {
                poses = new[]
                {
                    new RobotArmPose { poseName = "Idle", principalY = 0f, principalZ = 0f, brazo1Z = 0f, brazo2Z = 30f, brazo3Z = -20f, gancho1Z = -15f, gancho2Z = 15f },
                    new RobotArmPose { poseName = "ApproachPick", principalY = 25f, principalZ = -15f, brazo1Z = -40f, brazo2Z = 50f, brazo3Z = -30f, gancho1Z = -15f, gancho2Z = 15f },
                    new RobotArmPose { poseName = "GripClosed", principalY = 25f, principalZ = -15f, brazo1Z = -40f, brazo2Z = 50f, brazo3Z = -30f, gancho1Z = -50f, gancho2Z = 50f },
                    new RobotArmPose { poseName = "ApproachDrop", principalY = -35f, principalZ = 10f, brazo1Z = -20f, brazo2Z = 40f, brazo3Z = -10f, gancho1Z = -50f, gancho2Z = 50f },
                    new RobotArmPose { poseName = "GripOpen", principalY = -35f, principalZ = 10f, brazo1Z = -20f, brazo2Z = 40f, brazo3Z = -10f, gancho1Z = -15f, gancho2Z = 15f },
                    new RobotArmPose { poseName = "LiftHold", principalY = 25f, principalZ = -5f, brazo1Z = -40f, brazo2Z = 55f, brazo3Z = -15f, gancho1Z = -50f, gancho2Z = 50f },
                    new RobotArmPose { poseName = "ReachPick", principalY = 27f, principalZ = 12f, brazo1Z = 12f, brazo2Z = 8f, brazo3Z = -59f, gancho1Z = -15f, gancho2Z = 15f },
                    new RobotArmPose { poseName = "GripClosedAtReach", principalY = 27f, principalZ = 12f, brazo1Z = 12f, brazo2Z = 8f, brazo3Z = -59f, gancho1Z = -50f, gancho2Z = 50f }
                };
                return;
            }

            var list = new System.Collections.Generic.List<RobotArmPose>(poses);
            if (!TryGetPose("LiftHold", out _))
            {
                list.Add(new RobotArmPose
                {
                    poseName = "LiftHold",
                    principalY = 25f,
                    principalZ = -5f,
                    brazo1Z = -40f,
                    brazo2Z = 55f,
                    brazo3Z = -15f,
                    gancho1Z = -50f,
                    gancho2Z = 50f
                });
            }

            if (!TryGetPose("ReachPick", out _))
            {
                list.Add(new RobotArmPose
                {
                    poseName = "ReachPick",
                    principalY = 27f,
                    principalZ = 12f,
                    brazo1Z = 12f,
                    brazo2Z = 8f,
                    brazo3Z = -59f,
                    gancho1Z = -15f,
                    gancho2Z = 15f
                });
            }

            if (!TryGetPose("GripClosedAtReach", out _))
            {
                list.Add(new RobotArmPose
                {
                    poseName = "GripClosedAtReach",
                    principalY = 27f,
                    principalZ = 12f,
                    brazo1Z = 12f,
                    brazo2Z = 8f,
                    brazo3Z = -59f,
                    gancho1Z = -50f,
                    gancho2Z = 50f
                });
            }

            poses = list.ToArray();
        }
    }
}
