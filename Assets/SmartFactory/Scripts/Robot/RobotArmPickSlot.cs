using SmartFactory.Items;
using UnityEngine;

namespace SmartFactory.Robot
{
    /// <summary>
    /// 로봇팔별 컨베이어 집기 위치(앵커)와 허용 오차를 정의합니다.
    /// </summary>
    public class RobotArmPickSlot : MonoBehaviour
    {
        [SerializeField] RobotArmController arm;
        [SerializeField] Transform pickAnchor;
        [SerializeField] float pickToleranceX = 0.18f;
        [SerializeField] float pickToleranceZ = 0.25f;
        [SerializeField] float pickLineSlackX = 0.75f;

        /// <summary>연결된 로봇팔.</summary>
        public RobotArmController Arm => arm;

        /// <summary>집기 기준 Transform.</summary>
        public Transform PickAnchor => pickAnchor;

        /// <summary>
        /// 집기 앵커의 월드 X(벨트 +X 이동 기준).
        /// </summary>
        public float PickAnchorX => pickAnchor != null ? pickAnchor.position.x : transform.position.x;

        /// <summary>
        /// 빌더/에디터에서 참조를 설정합니다.
        /// </summary>
        public void Configure(RobotArmController linkedArm, Transform anchor)
        {
            arm = linkedArm;
            pickAnchor = anchor;
        }

        /// <summary>
        /// 물류가 집기 허용 범위에 들어왔는지 확인합니다.
        /// </summary>
        public bool IsItemInPickWindow(FactoryItem item)
        {
            if (item == null || pickAnchor == null)
            {
                return false;
            }

            var p = item.transform.position;
            var a = pickAnchor.position;
            return Mathf.Abs(p.x - a.x) <= pickToleranceX && Mathf.Abs(p.z - a.z) <= pickToleranceZ;
        }

        /// <summary>
        /// 물류가 집기 X를 지나쳐 더 이상 잡을 수 없는지 확인합니다.
        /// </summary>
        public bool HasItemPassedPickLine(FactoryItem item)
        {
            if (item == null || pickAnchor == null)
            {
                return false;
            }

            return item.transform.position.x > pickAnchor.position.x + pickToleranceX + pickLineSlackX;
        }
    }
}
