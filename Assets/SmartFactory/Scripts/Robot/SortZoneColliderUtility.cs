using UnityEngine;

namespace SmartFactory.Robot
{
    /// <summary>
    /// Normal / Unprocessed / Error Zone 공통 BoxCollider 크기·설정.
    /// </summary>
    public static class SortZoneColliderUtility
    {
        /// <summary>원통 Zone 메시와 동일한 지면 패드 두께(m).</summary>
        public const float ZoneVisualHeight = 0.1f;

        /// <summary>localScale (0.8, 0.05, 0.8) 원통과 동일한 월드 패드에 맞춘 로컬 Box size.</summary>
        public static readonly Vector3 BoxLocalSize = new Vector3(1f, 2f, 1f);

        /// <summary>
        /// CapsuleCollider를 제거하고 BoxCollider 하나만 둡니다.
        /// </summary>
        /// <param name="zoneGo">Zone GameObject.</param>
        /// <param name="isTrigger">트리거 여부(UnprocessedZone은 Awake에서 true로 재설정 가능).</param>
        public static void EnsureBoxCollider(GameObject zoneGo, bool isTrigger = false)
        {
            if (zoneGo == null)
            {
                return;
            }

            var capsules = zoneGo.GetComponents<CapsuleCollider>();
            for (var i = 0; i < capsules.Length; i++)
            {
                if (capsules[i] != null)
                {
                    Object.Destroy(capsules[i]);
                }
            }

            var box = zoneGo.GetComponent<BoxCollider>();
            if (box == null)
            {
                box = zoneGo.AddComponent<BoxCollider>();
            }

            box.size = BoxLocalSize;
            box.center = Vector3.zero;
            box.isTrigger = isTrigger;
        }
    }
}
