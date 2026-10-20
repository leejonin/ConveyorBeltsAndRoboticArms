using SmartFactory.Items;
using UnityEngine;

namespace SmartFactory.Robot
{
    /// <summary>
    /// 로봇팔별 분류 Zone Transform(Inspector 명시 연결, 이름 검색 없음).
    /// </summary>
    public class RobotArmZoneTargets : MonoBehaviour
    {
        [SerializeField] Transform normalZone;
        [SerializeField] Transform unprocessedZone;
        [SerializeField] Transform errorZone;

        /// <summary>
        /// 판정에 맞는 Zone Transform을 반환합니다.
        /// </summary>
        public bool TryResolve(ItemSortOutcome outcome, out Transform zone)
        {
            zone = outcome switch
            {
                ItemSortOutcome.Pass => normalZone,
                ItemSortOutcome.Fail => unprocessedZone,
                _ => errorZone
            };

            return zone != null;
        }

        /// <summary>
        /// SortZoneMap으로 비어 있는 슬롯을 채웁니다(폴백).
        /// </summary>
        public void ApplyFallbackFromMap(SortZoneMap map)
        {
            if (map == null)
            {
                return;
            }

            if (normalZone == null)
            {
                normalZone = map.defaultNormalZone;
            }

            if (unprocessedZone == null)
            {
                unprocessedZone = map.unprocessedZone;
            }

            if (errorZone == null)
            {
                errorZone = map.errorZone;
            }
        }

        /// <summary>
        /// 필수 Zone 참조가 모두 있는지 확인합니다.
        /// </summary>
        public bool HasCompleteSet()
        {
            return normalZone != null && unprocessedZone != null && errorZone != null;
        }
    }
}
