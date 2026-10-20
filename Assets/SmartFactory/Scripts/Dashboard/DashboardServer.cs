using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using SmartFactory.Brain;
using SmartFactory.Core;
using SmartFactory.Logistics;
using UnityEngine;

namespace SmartFactory.Dashboard
{
    /// <summary>
    /// HttpListener 기반 웹 제어판 서버(SSE 상태 스트림 포함).
    /// </summary>
    public class DashboardServer : MonoBehaviour
    {
        [SerializeField] FactoryConfig config;
        [SerializeField] BrainScheduler brainScheduler;
        [SerializeField] BrainCommandExecutor commandExecutor;

        HttpListener m_listener;
        CancellationTokenSource m_cts;
        string m_dashboardRoot;

        void Start()
        {
            m_dashboardRoot = Path.Combine(Application.streamingAssetsPath, "Dashboard");
            m_cts = new CancellationTokenSource();
            _ = StartServerAsync(m_cts.Token);
        }

        void OnDestroy()
        {
            m_cts?.Cancel();
            m_listener?.Close();
            m_listener = null;
        }

        async Task StartServerAsync(CancellationToken token)
        {
            var port = config != null ? config.dashboardPort : 8080;
            m_listener = new HttpListener();
            // Host 헤더가 접두사와 일치해야 함. localhost는 IPv6(::1)로 붙는 브라우저가 있음.
            m_listener.Prefixes.Add($"http://localhost:{port}/");
            m_listener.Prefixes.Add($"http://127.0.0.1:{port}/");
            m_listener.Prefixes.Add($"http://[::1]:{port}/");
            try
            {
                m_listener.Start();
                Debug.Log(
                    $"DashboardServer: http://127.0.0.1:{port}/ 또는 http://localhost:{port}/ (Play 중에만)");
            }
            catch (HttpListenerException ex)
            {
                Debug.LogError(
                    $"DashboardServer 시작 실패: {ex.Message}. Windows면 관리자 PowerShell에서 " +
                    $"netsh http add urlacl url=http://+:{port}/ user=Everyone 실행 후 재시도.");
                return;
            }

            while (!token.IsCancellationRequested)
            {
                HttpListenerContext context;
                try
                {
                    context = await m_listener.GetContextAsync();
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
                catch (HttpListenerException)
                {
                    return;
                }

                _ = Task.Run(() => HandleRequest(context), token);
            }
        }

        void HandleRequest(HttpListenerContext context)
        {
            try
            {
                var path = context.Request.Url.AbsolutePath.TrimEnd('/');
                if (string.IsNullOrEmpty(path))
                {
                    path = "/";
                }

                if (path == "/" || path.EndsWith(".html") || path.EndsWith(".js") || path.EndsWith(".css") || path.EndsWith(".svg"))
                {
                    ServeStatic(context, path);
                    return;
                }

                if (path == "/api/state")
                {
                    WriteJson(context, FactoryStateRegistry.Instance?.CreateSnapshot());
                    return;
                }

                if (path == "/api/settings" && context.Request.HttpMethod == "GET")
                {
                    var registry = FactoryStateRegistry.Instance;
                    WriteJson(context, new
                    {
                        intervalMinutes = brainScheduler != null ? brainScheduler.IntervalMinutes : 30,
                        consecutiveErrorAlertThreshold = registry != null
                            ? registry.Sorting.consecutiveErrorAlertThreshold
                            : 5
                    });
                    return;
                }

                if (path == "/api/settings" && context.Request.HttpMethod == "POST")
                {
                    using var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding);
                    var body = reader.ReadToEnd();
                    var payload = JsonConvert.DeserializeObject<SettingsPayload>(body);
                    if (payload != null)
                    {
                        MainThreadDispatcher.Instance.Enqueue(() =>
                        {
                            if (brainScheduler != null && payload.intervalMinutes > 0)
                            {
                                brainScheduler.SetIntervalMinutes(payload.intervalMinutes);
                            }

                            if (payload.consecutiveErrorAlertThreshold > 0)
                            {
                                FactoryStateRegistry.Instance?.SetConsecutiveErrorAlertThreshold(
                                    payload.consecutiveErrorAlertThreshold);
                            }
                        });
                    }

                    WriteJson(context, new { ok = true });
                    return;
                }

                if (path == "/api/brain/trigger" && context.Request.HttpMethod == "POST")
                {
                    MainThreadDispatcher.Instance.Enqueue(() => brainScheduler?.TriggerImmediate());
                    WriteJson(context, new { ok = true });
                    return;
                }

                if (path == "/api/robots/reset" && context.Request.HttpMethod == "POST")
                {
                    using var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding);
                    var body = reader.ReadToEnd();
                    var payload = JsonConvert.DeserializeObject<RobotResetPayload>(body);
                    var ok = false;
                    if (payload != null && !string.IsNullOrEmpty(payload.id) && commandExecutor != null)
                    {
                        MainThreadDispatcher.Instance.Enqueue(() =>
                        {
                            commandExecutor.ResetRobot(payload.id);
                        });
                        ok = true;
                    }

                    WriteJson(context, new { ok });
                    return;
                }

                if (path == "/api/stream")
                {
                    HandleSse(context);
                    return;
                }

                if (path == "/api/ops/report")
                {
                    var center = UnityEngine.Object.FindFirstObjectByType<FactoryLogisticsCenter>();
                    var report = center != null ? center.BuildOpsReport(false) : new OpsReportDto();
                    WriteJson(context, report);
                    return;
                }

                if (path == "/api/packages/priority-log")
                {
                    var snap = FactoryStateRegistry.Instance?.Logistics;
                    WriteJson(context, snap?.priorityChanges ?? new System.Collections.Generic.List<PriorityChangeLogDto>());
                    return;
                }

                if (path == "/api/logistics/mitigate" && context.Request.HttpMethod == "POST")
                {
                    MainThreadDispatcher.Instance.Enqueue(() =>
                    {
                        UnityEngine.Object.FindFirstObjectByType<FactoryLogisticsCenter>()?.ApplyCongestionMitigationSuggestion();
                    });
                    WriteJson(context, new { ok = true });
                    return;
                }

                context.Response.StatusCode = 404;
                context.Response.Close();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Dashboard 요청 처리 오류: {ex.Message}");
                try
                {
                    context.Response.StatusCode = 500;
                    context.Response.Close();
                }
                catch
                {
                    // ignored
                }
            }
        }

        void HandleSse(HttpListenerContext context)
        {
            context.Response.ContentType = "text/event-stream";
            context.Response.Headers.Add("Cache-Control", "no-cache");
            context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
            var output = context.Response.OutputStream;
            var interval = config != null ? config.dashboardSseIntervalSeconds : 1f;

            for (var i = 0; i < 3600; i++)
            {
                var snapshot = FactoryStateRegistry.Instance?.CreateSnapshot();
                var json = JsonConvert.SerializeObject(snapshot);
                var bytes = Encoding.UTF8.GetBytes($"data: {json}\n\n");
                output.Write(bytes, 0, bytes.Length);
                output.Flush();
                Thread.Sleep(Mathf.RoundToInt(interval * 1000f));
            }

            context.Response.Close();
        }

        void ServeStatic(HttpListenerContext context, string path)
        {
            var relative = path == "/" ? "index.html" : path.TrimStart('/');
            var filePath = Path.Combine(m_dashboardRoot, relative.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(filePath))
            {
                context.Response.StatusCode = 404;
                context.Response.Close();
                return;
            }

            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            context.Response.ContentType = ext switch
            {
                ".html" => "text/html; charset=utf-8",
                ".js" => "application/javascript; charset=utf-8",
                ".css" => "text/css; charset=utf-8",
                ".svg" => "image/svg+xml",
                _ => "application/octet-stream"
            };

            var data = File.ReadAllBytes(filePath);
            context.Response.OutputStream.Write(data, 0, data.Length);
            context.Response.Close();
        }

        static void WriteJson(HttpListenerContext context, object payload)
        {
            var json = JsonConvert.SerializeObject(payload);
            var buffer = Encoding.UTF8.GetBytes(json);
            context.Response.ContentType = "application/json; charset=utf-8";
            context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
            context.Response.OutputStream.Write(buffer, 0, buffer.Length);
            context.Response.Close();
        }

        [Serializable]
        class SettingsPayload
        {
            public int intervalMinutes;
            public int consecutiveErrorAlertThreshold;
        }

        [Serializable]
        class RobotResetPayload
        {
            public string id;
        }
    }
}
