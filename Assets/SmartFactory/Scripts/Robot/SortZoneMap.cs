using SmartFactory.Core;
using UnityEngine;

namespace SmartFactory.Robot
{
    /// <summary>
    /// 일치 물품의 정상 분류 구역 매핑(임시 기본: 단일 구역).
    /// </summary>
    [CreateAssetMenu(fileName = "SortZoneMap", menuName = "SmartFactory/Sort Zone Map")]
    public class SortZoneMap : ScriptableObject
    {
        public Transform defaultNormalZone;
        public Transform errorZone;
        public Transform unprocessedZone;
    }
}
