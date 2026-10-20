namespace SmartFactory.Robot
{
    /// <summary>
    /// 로봇팔 배터리를 시뮬레이션합니다.
    /// </summary>
    public class BatterySimulator
    {
        float m_percent = 100f;

        /// <summary>
        /// 현재 배터리 퍼센트.
        /// </summary>
        public float Percent => m_percent;

        /// <summary>
        /// 작업으로 배터리를 소모합니다.
        /// </summary>
        public void Consume(float amount)
        {
            m_percent = System.Math.Max(0f, m_percent - amount);
        }

        /// <summary>
        /// 대기 중 충전합니다.
        /// </summary>
        public void RechargeIdle(float deltaTime)
        {
            m_percent = System.Math.Min(100f, m_percent + deltaTime * 0.5f);
        }
    }
}
