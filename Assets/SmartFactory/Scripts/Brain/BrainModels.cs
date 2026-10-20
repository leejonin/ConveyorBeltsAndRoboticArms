using System;
using System.Collections.Generic;

namespace SmartFactory.Brain
{
    /// <summary>
    /// 중앙뇌 명령 항목.
    /// </summary>
    [Serializable]
    public class BrainAction
    {
        public string target;
        public string command;
        public string args;
    }

    /// <summary>
    /// 상급자/정비 알림 항목.
    /// </summary>
    [Serializable]
    public class BrainEscalation
    {
        public string to;
        public string message;
    }

    /// <summary>
    /// 중앙뇌 판단 결과.
    /// </summary>
    [Serializable]
    public class BrainDecision
    {
        public string report;
        public string brainState;
        public List<BrainAction> actions = new List<BrainAction>();
        public List<BrainEscalation> escalations = new List<BrainEscalation>();
    }
}
