namespace SmartFactory.Items
{
    /// <summary>
    /// 에러 물류(스폰 단계에서 주입된 이상 물류)의 유형. ErrorZone으로 분류됩니다.
    /// </summary>
    public enum ErrorLogisticsKind
    {
        /// <summary>일반 물류.</summary>
        None = 0,

        /// <summary>QR 페이로드가 비정상·판독 불가.</summary>
        InvalidQr = 1,

        /// <summary>QR 라벨 없음.</summary>
        MissingQr = 2,

        /// <summary>비정상 메시 형태.</summary>
        AbnormalMesh = 3
    }
}
