using SmartFactory.Core;
using SmartFactory.Items;
using SmartFactory.Recognition;
using UnityEngine;

namespace SmartFactory.Conveyor
{
    /// <summary>
    /// 컨베이어 테스트: 물류·QR 선택 생성 및 스캔 결과 표시(OnGUI).
    /// </summary>
    public class ConveyorTestHarness : MonoBehaviour
    {
        const float PanelWidth = 320f;
        const float PanelMaxHeight = 520f;

        [SerializeField] ItemSpawner spawner;
        [SerializeField] bool showPanel = true;
        [SerializeField] KeyCode togglePanelKey = KeyCode.F1;

        static readonly ShapeType[] s_shapes =
        {
            ShapeType.Circle,
            ShapeType.Cube,
            ShapeType.Triangle
        };

        static readonly string[] s_shapeLabels =
        {
            "Circle (원)",
            "Cube (큐브)",
            "Triangle (삼각)"
        };

        int m_meshShapeIndex;
        int m_qrShapeIndex;
        int m_previewId = 1;
        Vector2 m_scroll;
        Rect m_panelRect = new Rect(12f, 12f, PanelWidth, PanelMaxHeight);

        bool m_hasScan;
        ConveyorScanReport m_lastScan;
        FactoryItem m_lastSpawned;

        void Start()
        {
            if (spawner == null)
            {
                spawner = FindFirstObjectByType<ItemSpawner>();
            }

            ClampPanelToScreen();
        }

        void Update()
        {
            if (Input.GetKeyDown(togglePanelKey))
            {
                showPanel = !showPanel;
            }
        }

        void OnGUI()
        {
            if (!showPanel)
            {
                return;
            }

            ClampPanelToScreen();
            m_panelRect = GUI.Window(91025, m_panelRect, DrawPanelWindow, "컨베이어 테스트");
        }

        void DrawPanelWindow(int id)
        {
            var contentHeight = PanelMaxHeight - 28f;
            m_scroll = GUILayout.BeginScrollView(m_scroll, GUILayout.Width(m_panelRect.width - 8f), GUILayout.Height(contentHeight));

            if (spawner == null)
            {
                GUILayout.Label("ItemSpawner가 연결되지 않았습니다.");
                GUILayout.EndScrollView();
                GUI.DragWindow(new Rect(0f, 0f, m_panelRect.width, 22f));
                return;
            }

            GUILayout.Label("물류 생성", BoldLabel());
            m_meshShapeIndex = GUILayout.SelectionGrid(m_meshShapeIndex, s_shapeLabels, 1, GUILayout.MaxWidth(PanelWidth - 24f));
            GUILayout.Space(4f);
            GUILayout.Label("QR 코드 내용 (ShapeType)", BoldLabel());
            m_qrShapeIndex = GUILayout.SelectionGrid(m_qrShapeIndex, s_shapeLabels, 1, GUILayout.MaxWidth(PanelWidth - 24f));

            var meshShape = s_shapes[m_meshShapeIndex];
            var qrShape = s_shapes[m_qrShapeIndex];
            var payload = QrLabelGenerator.BuildPayload(m_previewId, qrShape);
            GUILayout.Label($"다음 ID: {m_previewId}", SmallLabel());
            GUILayout.Label($"QR 페이로드: {payload}", SmallLabel());

            if (GUILayout.Button("생성"))
            {
                m_lastSpawned = spawner.SpawnExplicit(meshShape, qrShape);
                if (m_lastSpawned != null)
                {
                    m_previewId = m_lastSpawned.ItemId + 1;
                }
            }

            GUILayout.Space(6f);
            GUILayout.Label("스폰 상태", BoldLabel());
            if (m_lastSpawned != null && m_lastSpawned.gameObject.activeInHierarchy)
            {
                GUILayout.Label($"ID {m_lastSpawned.ItemId} · Mesh={m_lastSpawned.MeshShape} · QR={m_lastSpawned.QrShape}");
            }
            else
            {
                GUILayout.Label("(없음)");
            }

            GUILayout.Label($"자동 스폰: {(spawner.AutoSpawnEnabled ? "켜짐" : "꺼짐")}", SmallLabel());

            GUILayout.Space(6f);
            DrawScanReportSection();

            GUILayout.Space(4f);
            GUILayout.Label($"{togglePanelKey}: 패널 · QrScanZone 통과 시 스캔", SmallLabel());
            GUILayout.EndScrollView();
            GUI.DragWindow(new Rect(0f, 0f, m_panelRect.width, 22f));
        }

        void DrawScanReportSection()
        {
            GUILayout.Label("센서 / QR 판독", BoldLabel());
            if (!m_hasScan)
            {
                GUILayout.Label("스캔 존에 물류를 보내세요.");
                return;
            }

            var r = m_lastScan;
            GUILayout.Label($"[기대] ID={r.itemId} Mesh={r.expectedMeshShape} QR={r.expectedQrShape}", SmallLabel());

            GUILayout.Space(2f);
            GUILayout.Label("실제 QR (ZXing)", BoldLabel());
            if (r.qrScan.success)
            {
                GUILayout.Label($"성공 · shape={r.qrScan.shape} id={r.qrScan.itemId}");
                GUILayout.Label($"raw: {r.qrScan.rawText}", SmallLabel());
            }
            else
            {
                GUILayout.Label("실패 · 판독 없음");
            }

            GUILayout.Space(2f);
            GUILayout.Label("Mock 비전 (MockShapeClassifier)", BoldLabel());
            if (r.mockVisionClassified)
            {
                GUILayout.Label($"shape={r.mockVisionShape} conf={r.mockVisionConfidence:F2}");
            }
            else
            {
                GUILayout.Label("분류 실패");
            }

            GUILayout.Space(2f);
            GUILayout.Label($"QR ↔ Mock 일치: {r.qrMatchesMockVision}");
            GUILayout.Label($"QR ↔ 기대 QR 일치: {ShapeComparator.IsMatch(r.qrScan.success ? r.qrScan.shape : r.expectedQrShape, r.expectedQrShape)}");
            GUILayout.Label($"QR ↔ Mesh 일치: {r.qrMatchesMeshShape}");
        }

        /// <summary>
        /// 스캔 존에서 호출: 최신 결과 저장.
        /// </summary>
        public void ReportScan(ConveyorScanReport report)
        {
            m_lastScan = report;
            m_hasScan = true;
        }

        /// <summary>
        /// 스포너 참조를 설정합니다.
        /// </summary>
        public void SetSpawner(ItemSpawner itemSpawner)
        {
            spawner = itemSpawner;
        }

        void ClampPanelToScreen()
        {
            m_panelRect.width = PanelWidth;
            m_panelRect.height = Mathf.Min(PanelMaxHeight, Screen.height - 24f);
            m_panelRect.x = Mathf.Clamp(m_panelRect.x, 0f, Mathf.Max(0f, Screen.width - m_panelRect.width));
            m_panelRect.y = Mathf.Clamp(m_panelRect.y, 0f, Mathf.Max(0f, Screen.height - m_panelRect.height));
        }

        static GUIStyle BoldLabel()
        {
            return new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
        }

        static GUIStyle SmallLabel()
        {
            return new GUIStyle(GUI.skin.label) { fontSize = 11, wordWrap = true };
        }
    }
}
