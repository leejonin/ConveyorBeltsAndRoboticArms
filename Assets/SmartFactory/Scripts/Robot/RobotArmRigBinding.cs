using UnityEngine;

namespace SmartFactory.Robot
{
    /// <summary>
    /// FBX 리그 본 경로 및 배치 오프셋. Transform 참조 대신 계층 경로(이름)로 런타임 해석합니다.
    /// </summary>
    [CreateAssetMenu(fileName = "RobotArmRigBinding", menuName = "SmartFactory/Robot Arm Rig Binding")]
    public class RobotArmRigBinding : ScriptableObject
    {
        [Header("배치 보정 (팔 루트 로컬)")]
        public Vector3 localPositionOffset;
        public Vector3 localEulerOffset;
        public Vector3 localScale = Vector3.one;

        [Header("본 경로 (visualRoot 기준, / 로 구분 또는 단일 이름은 깊이 검색)")]
        [Tooltip("비어 있으면 모델 인스턴스 루트")]
        public string visualRootPath;

        [Tooltip("레거시 IK 관절 경로. 회전 제어는 motionPaths 사용")]
        public string[] jointPaths;

        [Header("회전 제어 (로봇팔_움직임.md)")]
        public string principalPath = "Principal";
        public string brazo1Path = "Brazo1";
        public string brazo2Path = "Brazo2";
        public string brazo3Path = "Brazo3";
        public string gancho1Path = "Gancho1";
        public string gancho2Path = "Gancho2";
        public string endEffectorPath = "Piston_end";
        public string gripCenterPath = "CenterOfTheGripper";

        [Header("각도 제한 (로컬 축, 도)")]
        public float principalYMin = -360f;
        public float principalYMax = 360f;
        public float principalZMin = -90f;
        public float principalZMax = 90f;
        public float brazoZMin = -120f;
        public float brazoZMax = 120f;
        public float gancho1ZMin = -50f;
        public float gancho1ZMax = -15f;
        public float gancho2ZMin = 15f;
        public float gancho2ZMax = 50f;

        /// <summary>
        /// 모델 루트에서 IK용 Transform을 해석합니다(레거시).
        /// </summary>
        public bool TryResolve(Transform modelRoot, out Transform[] joints, out Transform endEffector)
        {
            joints = null;
            endEffector = null;
            if (modelRoot == null)
            {
                return false;
            }

            if (!TryResolveMotionRig(modelRoot, out var rig))
            {
                return LegacyTryResolve(this, modelRoot, out joints, out endEffector);
            }

            joints = new[] { rig.Principal, rig.Brazo1, rig.Brazo2, rig.Brazo3 };
            endEffector = rig.EndEffector;
            return true;
        }

        /// <summary>
        /// 문서 기준 회전 제어 본을 해석합니다.
        /// </summary>
        public bool TryResolveMotionRig(Transform modelRoot, out RobotArmMotionRig rig)
        {
            rig = new RobotArmMotionRig();
            if (modelRoot == null)
            {
                return false;
            }

            var visualRoot = string.IsNullOrEmpty(visualRootPath)
                ? modelRoot
                : FindTransform(modelRoot, visualRootPath);
            if (visualRoot == null)
            {
                return false;
            }

            var principal = FindTransform(visualRoot, principalPath);
            if (principal == null)
            {
                return false;
            }

            rig.Principal = principal;
            rig.Brazo1 = ResolveInSubtree(principal, brazo1Path) ?? FindTransform(visualRoot, brazo1Path);
            rig.Brazo2 = rig.Brazo1 != null
                ? ResolveInSubtree(rig.Brazo1, brazo2Path) ?? FindTransform(visualRoot, brazo2Path)
                : FindTransform(visualRoot, brazo2Path);
            rig.Brazo3 = rig.Brazo2 != null
                ? ResolveInSubtree(rig.Brazo2, brazo3Path) ?? FindTransform(visualRoot, brazo3Path)
                : FindTransform(visualRoot, brazo3Path);
            rig.Gancho1 = rig.Brazo3 != null
                ? ResolveInSubtree(rig.Brazo3, gancho1Path) ?? FindTransform(visualRoot, gancho1Path)
                : FindTransform(visualRoot, gancho1Path);
            rig.Gancho2 = rig.Brazo3 != null
                ? ResolveInSubtree(rig.Brazo3, gancho2Path) ?? FindTransform(visualRoot, gancho2Path)
                : FindTransform(visualRoot, gancho2Path);
            rig.EndEffector = FindTransform(visualRoot, endEffectorPath);
            rig.GripCenter = string.IsNullOrEmpty(gripCenterPath)
                ? null
                : FindTransform(visualRoot, gripCenterPath);
            rig.ArmRigMonitor = FindTransform(visualRoot, "Arm Rig");
            rig.BaseMonitor = FindTransform(visualRoot, "Base");

            if (!rig.IsValid)
            {
                return false;
            }

            return true;
        }

        static bool LegacyTryResolve(RobotArmRigBinding binding, Transform modelRoot, out Transform[] joints, out Transform endEffector)
        {
            joints = null;
            endEffector = null;
            var visualRoot = modelRoot;
            if (binding.jointPaths == null || binding.jointPaths.Length == 0 || string.IsNullOrEmpty(binding.endEffectorPath))
            {
                return false;
            }

            var resolved = new Transform[binding.jointPaths.Length];
            for (var i = 0; i < binding.jointPaths.Length; i++)
            {
                resolved[i] = FindTransform(visualRoot, binding.jointPaths[i]);
                if (resolved[i] == null)
                {
                    return false;
                }
            }

            endEffector = FindTransform(visualRoot, binding.endEffectorPath);
            if (endEffector == null)
            {
                return false;
            }

            joints = resolved;
            return true;
        }

        static Transform ResolveInSubtree(Transform root, string name)
        {
            if (root == null || string.IsNullOrEmpty(name))
            {
                return null;
            }

            return FindDeepByName(root, name);
        }

        /// <summary>
        /// 경로 또는 이름으로 Transform을 찾습니다.
        /// </summary>
        public static Transform FindTransform(Transform root, string pathOrName)
        {
            if (root == null || string.IsNullOrEmpty(pathOrName))
            {
                return null;
            }

            if (pathOrName.Contains("/"))
            {
                var current = root;
                var parts = pathOrName.Split('/');
                foreach (var part in parts)
                {
                    if (string.IsNullOrEmpty(part))
                    {
                        continue;
                    }

                    current = current.Find(part);
                    if (current == null)
                    {
                        return null;
                    }
                }

                return current;
            }

            return FindDeepByName(root, pathOrName);
        }

        static Transform FindDeepByName(Transform current, string name)
        {
            if (current.name == name)
            {
                return current;
            }

            for (var i = 0; i < current.childCount; i++)
            {
                var found = FindDeepByName(current.GetChild(i), name);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }
    }
}
