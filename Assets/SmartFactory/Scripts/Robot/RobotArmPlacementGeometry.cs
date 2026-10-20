using SmartFactory.Items;
using UnityEngine;

namespace SmartFactory.Robot
{
    /// <summary>
    /// Zone 배치 시 grip center 목표 월드 좌표 계산(Zone Transform Y를 grip 높이로 직접 쓰지 않음).
    /// </summary>
    public static class RobotArmPlacementGeometry
    {
        /// <summary>
        /// Zone 상공 운반 높이의 grip 목표점.
        /// </summary>
        public static Vector3 ComputeTravelGripPoint(Transform zone, float carryClearanceY)
        {
            var p = zone != null ? zone.position : Vector3.zero;
            return new Vector3(p.x, p.y + carryClearanceY, p.z);
        }

        /// <summary>
        /// Zone 지지면 위 배치 높이의 grip 목표점(잡은 시점 grip↔PickAlign 오프셋 유지).
        /// </summary>
        public static Vector3 ComputePlaceGripPoint(
            Transform zone,
            FactoryItem item,
            Vector3 gripToPickAlignOffsetWorld,
            float zoneSurfaceYOffset)
        {
            var zonePos = zone != null ? zone.position : Vector3.zero;
            var surfaceY = zonePos.y + zoneSurfaceYOffset;
            var halfItemY = 0.05f;
            var bodyCol = item != null ? item.BodyCollider : null;
            if (bodyCol != null)
            {
                halfItemY = bodyCol.bounds.extents.y;
            }

            var pickAlignTarget = new Vector3(zonePos.x, surfaceY + halfItemY, zonePos.z);
            return pickAlignTarget + gripToPickAlignOffsetWorld;
        }
    }
}
