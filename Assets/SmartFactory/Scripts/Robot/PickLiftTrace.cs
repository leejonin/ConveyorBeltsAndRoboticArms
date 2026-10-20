using System;
using System.IO;
using UnityEngine;

namespace SmartFactory.Robot
{
    /// <summary>
    /// 집기·들어올리기 시도 단위 NDJSON 추적 (프로젝트 루트 pick-lift-trace.log).
    /// </summary>
    public static class PickLiftTrace
    {
        const string LogFileName = "pick-lift-trace.log";

        static int s_runSeq;
        static int s_activeRunId = -1;
        static int s_activeArmIndex = -1;
        static int s_activeItemId = -1;

        /// <summary>Play 중 trace 기록 여부.</summary>
        public static bool Enabled { get; set; } = true;

        /// <summary>현재 추적 중인 runId(-1 없음).</summary>
        public static int ActiveRunId => s_activeRunId;

        /// <summary>현재 run의 ItemId.</summary>
        public static int ActiveItemId => s_activeItemId;

        static string LogPath =>
            Path.Combine(Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath, LogFileName);

        /// <summary>
        /// 새 집기 시도 run을 시작하고 runId를 반환합니다.
        /// </summary>
        public static int BeginRun(int armIndex, int itemId)
        {
            s_runSeq++;
            s_activeRunId = s_runSeq;
            s_activeArmIndex = armIndex;
            s_activeItemId = itemId;
            WriteLine(s_activeRunId, armIndex, itemId, "RunBegin", null, null);
            return s_activeRunId;
        }

        /// <summary>
        /// run을 종료합니다.
        /// </summary>
        public static void EndRun(string phase, string reason = null)
        {
            if (s_activeRunId < 0)
            {
                return;
            }

            WriteLine(s_activeRunId, s_activeArmIndex, s_activeItemId, phase, reason, null);
            s_activeRunId = -1;
            s_activeArmIndex = -1;
            s_activeItemId = -1;
        }

        /// <summary>
        /// phase 전환·샘플 이벤트를 기록합니다.
        /// </summary>
        public static void LogPhase(int runId, int armIndex, int itemId, string phase, string dataJson = null)
        {
            if (!Enabled || runId < 0)
            {
                return;
            }

            WriteLine(runId, armIndex, itemId, phase, null, dataJson);
        }

        static void WriteLine(int runId, int armIndex, int itemId, string phase, string reason, string dataJson)
        {
            if (!Enabled)
            {
                return;
            }

            try
            {
                if (string.IsNullOrEmpty(dataJson))
                {
                    dataJson = "{}";
                }

                var ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                var reasonPart = string.IsNullOrEmpty(reason)
                    ? ""
                    : ",\"reason\":\"" + Escape(reason) + "\"";
                var line =
                    "{\"timestamp\":" + ts + ",\"runId\":" + runId + ",\"armIndex\":" + armIndex +
                    ",\"itemId\":" + itemId + ",\"phase\":\"" + Escape(phase) + "\"" + reasonPart +
                    ",\"data\":" + dataJson + "}";
                File.AppendAllText(LogPath, line + "\n");
            }
            catch
            {
                // ignore logging failures
            }
        }

        static string Escape(string s) =>
            string.IsNullOrEmpty(s) ? "" : s.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
