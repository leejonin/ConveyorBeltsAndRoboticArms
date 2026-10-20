namespace SmartFactory.Items
{
    /// <summary>
    /// 스테이션 인식·비교 후 물류 분류 판정(Zone 목적지 매핑용).
    /// </summary>
    public enum ItemSortOutcome
    {
        /// <summary>QR·분류 일치 — NormalZone.</summary>
        Pass = 0,

        /// <summary>QR·분류 불일치 — UnprocessedZone.</summary>
        Fail = 1,

        /// <summary>인식/분류 실패·신뢰도 미달·에러 물류 — ErrorZone.</summary>
        Error = 2
    }
}
