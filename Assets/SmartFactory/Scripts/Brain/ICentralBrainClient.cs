using System.Threading;
using System.Threading.Tasks;
using SmartFactory.Core;

namespace SmartFactory.Brain
{
    /// <summary>
    /// 중앙뇌 LLM API 클라이언트 인터페이스.
    /// </summary>
    public interface ICentralBrainClient
    {
        /// <summary>
        /// 공장 스냅샷을 전달하고 판단 결과를 받습니다.
        /// </summary>
        Task<BrainDecision> RequestAsync(FactorySnapshot snapshot, CancellationToken cancellationToken);
    }
}
