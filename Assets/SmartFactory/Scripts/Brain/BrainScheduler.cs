using System;
using System.Threading;
using System.Threading.Tasks;
using SmartFactory.Core;
using UnityEngine;

namespace SmartFactory.Brain
{
    /// <summary>
    /// 선택 주기에 따라 중앙뇌 API를 호출합니다.
    /// </summary>
    public class BrainScheduler : MonoBehaviour
    {
        [SerializeField] FactoryConfig config;
        [SerializeField] BrainCommandExecutor executor;

        ICentralBrainClient m_client;
        CancellationTokenSource m_cts;
        int m_intervalMinutes;
        DateTime m_nextCallUtc;

        public int IntervalMinutes => m_intervalMinutes;

        void Awake()
        {
            m_client = new MockCentralBrainClient();
            m_intervalMinutes = config != null ? config.defaultBrainIntervalMinutes : 30;
            m_nextCallUtc = DateTime.UtcNow.AddMinutes(m_intervalMinutes);
            PublishBrainStatus("Ready", "초기화 완료");
        }

        void OnEnable()
        {
            m_cts = new CancellationTokenSource();
            _ = RunLoopAsync(m_cts.Token);
        }

        void OnDisable()
        {
            m_cts?.Cancel();
            m_cts?.Dispose();
            m_cts = null;
        }

        /// <summary>
        /// API 호출 주기(분)를 변경합니다.
        /// </summary>
        public void SetIntervalMinutes(int minutes)
        {
            m_intervalMinutes = minutes;
            m_nextCallUtc = DateTime.UtcNow.AddMinutes(minutes);
            PublishBrainStatus("Ready", $"호출 주기 {minutes}분으로 변경");
        }

        /// <summary>
        /// 디버그용 즉시 호출.
        /// </summary>
        public async void TriggerImmediate()
        {
            await CallBrainAsync(m_cts.Token);
        }

        async Task RunLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                var delay = m_nextCallUtc - DateTime.UtcNow;
                if (delay.TotalMilliseconds > 0)
                {
                    try
                    {
                        await Task.Delay(delay, token);
                    }
                    catch (TaskCanceledException)
                    {
                        return;
                    }
                }

                await CallBrainAsync(token);
                m_nextCallUtc = DateTime.UtcNow.AddMinutes(m_intervalMinutes);
            }
        }

        async Task CallBrainAsync(CancellationToken token)
        {
            if (FactoryStateRegistry.Instance == null)
            {
                return;
            }

            PublishBrainStatus("Calling", "상태 분석 중...");
            var snapshot = FactoryStateRegistry.Instance.CreateSnapshot();
            var decision = await m_client.RequestAsync(snapshot, token);
            executor?.Execute(decision);
            PublishBrainStatus(decision.brainState, decision.report);
        }

        void PublishBrainStatus(string state, string report)
        {
            FactoryStateRegistry.Instance?.UpdateBrain(new BrainStatus
            {
                state = state,
                lastReport = report,
                nextCallUtc = m_nextCallUtc.ToString("o"),
                intervalMinutes = m_intervalMinutes
            });
        }
    }
}
