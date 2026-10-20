const robotsPanel = document.getElementById("robotsPanel");
const conveyorPanel = document.getElementById("conveyorPanel");
const brainPanel = document.getElementById("brainPanel");
const notifications = document.getElementById("notifications");
const intervalSelect = document.getElementById("intervalSelect");
const consecutiveErrorThresholdSelect = document.getElementById("consecutiveErrorThresholdSelect");
const triggerBrain = document.getElementById("triggerBrain");
const emergencyBanner = document.getElementById("emergencyBanner");
const emergencyBannerText = document.getElementById("emergencyBannerText");
const logisticsPanel = document.getElementById("logisticsPanel");
const opsReportEl = document.getElementById("opsReport");
const mitigateCongestion = document.getElementById("mitigateCongestion");
const loadOpsReport = document.getElementById("loadOpsReport");

async function resetRobot(robotId) {
  if (!confirm(`${robotId} 로봇팔을 긴급 초기화할까요?`)) {
    return;
  }
  const res = await fetch("/api/robots/reset", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ id: robotId }),
  });
  const data = await res.json().catch(() => ({}));
  if (!data.ok) {
    alert("초기화 요청에 실패했습니다.");
  }
}

function renderSnapshot(snapshot) {
  if (!snapshot) return;
  document.getElementById("mismatchCount").textContent = snapshot.sorting?.mismatchCount ?? 0;
  document.getElementById("recognitionFailureCount").textContent = snapshot.sorting?.recognitionFailureCount ?? 0;
  document.getElementById("unprocessedCount").textContent = snapshot.sorting?.unprocessedCount ?? 0;
  const sorting = snapshot.sorting ?? {};
  document.getElementById("errorLogisticsCount").textContent = sorting.errorLogisticsCount ?? 0;
  const consecutive = sorting.consecutiveErrorLogisticsCount ?? 0;
  const threshold = sorting.consecutiveErrorAlertThreshold ?? 5;
  const consecutiveEl = document.getElementById("consecutiveErrorLogisticsCount");
  consecutiveEl.textContent =
    consecutive + (consecutive >= threshold ? " (알림 구간)" : "") +
    (sorting.errorLogisticsBurstActive ? " · 버스트" : "");

  const lh = snapshot.lineHealth ?? {};
  const stalledSet = new Set(lh.stalledRobotIds ?? []);
  if (lh.emergencyActive) {
    emergencyBanner.classList.remove("hidden");
    emergencyBannerText.textContent =
      lh.emergencyMessage ?? "벨트 물류 정체 및 로봇팔 무움직임이 감지되었습니다.";
  } else {
    emergencyBanner.classList.add("hidden");
    emergencyBannerText.textContent = "";
  }

  const c = snapshot.conveyor ?? {};
  conveyorPanel.innerHTML = `
    <p>구동: ${c.isRunning ? "가동" : "정지"}</p>
    <p>속도: ${(c.speed ?? 0).toFixed(2)}</p>
    <p>오류: ${c.errorDetail ?? "-"}</p>
    <p>물류 정체: ${lh.logisticsStagnation ? "감지" : "없음"} (${(lh.logisticsStagnationSeconds ?? 0).toFixed(1)}s)</p>
  `;

  robotsPanel.innerHTML = "";
  (snapshot.robots ?? []).forEach((r) => {
    const card = document.createElement("div");
    card.className = "card" + (stalledSet.has(r.id) || r.isMovementStalled ? " stalled" : "");
    card.innerHTML = `
      <h3>${r.id}</h3>
      <p>상태: ${r.currentState}</p>
      <p>작업: ${r.currentTask} (${Math.round((r.taskProgress ?? 0) * 100)}%)</p>
      <p>완료: ${r.completionState}</p>
      <p>배터리: ${(r.batteryPercent ?? 0).toFixed(1)}%</p>
      <p>정상: ${r.isNormal ? "정상" : "문제"} / 버그: ${r.hasBug ? "있음" : "없음"}</p>
      <p>무움직임: ${r.isMovementStalled ? "정체 (" + (r.secondsSinceEndEffectorMove ?? 0).toFixed(1) + "s)" : "정상"}</p>
      <p>구역: ${r.regionId ?? "-"} / 팔#${r.armIndexInRegion ?? "-"}</p>
      <p>오류: ${r.errorDetail ?? "-"}</p>
    `;
    const resetBtn = document.createElement("button");
    resetBtn.type = "button";
    resetBtn.className = "reset-arm";
    resetBtn.textContent = "로봇팔 초기화";
    resetBtn.addEventListener("click", () => resetRobot(r.id));
    card.appendChild(resetBtn);
    robotsPanel.appendChild(card);
  });

  const b = snapshot.brain ?? {};
  brainPanel.innerHTML = `
    <p>상태: ${b.state ?? "-"}</p>
    <p>보고: ${b.lastReport ?? "-"}</p>
    <p>다음 호출(UTC): ${b.nextCallUtc ?? "-"}</p>
    <p>주기: ${b.intervalMinutes ?? "-"}분</p>
  `;

  const log = snapshot.logistics ?? {};
  const regions = (log.regions ?? [])
    .map(
      (rg) =>
        `<li>${rg.region}: 대기열 ${rg.queueLength}, 혼잡 ${rg.congestionLevel}, 처리량 ${(rg.throughputPerMinute ?? 0).toFixed(1)}/분 (가상)</li>`
    )
    .join("");
  const waiting = (log.packagesWaiting ?? [])
    .slice(0, 8)
    .map(
      (p) =>
        `<li>${p.packageId} → ${p.destination} [${p.priority}] 마감 ${(p.minutesToDeadline ?? 0).toFixed(1)}분 / ${p.delayRisk ?? "-"}</li>`
    )
    .join("");
  logisticsPanel.innerHTML = `
    <p>스키마 v${log.schemaVersion ?? 1} · 가상 온도 ${(log.virtualTemperatureC ?? 0).toFixed(1)}°C · 에너지 ${(log.virtualEnergyKwh ?? 0).toFixed(3)} kWh (시뮬)</p>
    <p>${log.ops?.summaryText ?? ""}</p>
    <h3>구역 혼잡</h3><ul>${regions || "<li>없음</li>"}</ul>
    <h3>대기 택배</h3><ul>${waiting || "<li>없음</li>"}</ul>
  `;

  notifications.innerHTML = "";
  (snapshot.notifications ?? []).forEach((n) => {
    const li = document.createElement("li");
    if (n.severity === "urgent") {
      li.className = "urgent";
    }
    li.textContent = `[${n.timestampUtc}] ${n.target}: ${n.message}`;
    notifications.appendChild(li);
  });
}

const source = new EventSource("/api/stream");
source.onmessage = (event) => {
  try {
    renderSnapshot(JSON.parse(event.data));
  } catch (e) {
    console.error(e);
  }
};

async function postSettings() {
  await fetch("/api/settings", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({
      intervalMinutes: Number(intervalSelect.value),
      consecutiveErrorAlertThreshold: Number(consecutiveErrorThresholdSelect.value),
    }),
  });
}

intervalSelect.addEventListener("change", postSettings);
consecutiveErrorThresholdSelect.addEventListener("change", postSettings);

fetch("/api/settings")
  .then((r) => r.json())
  .then((s) => {
    if (s.intervalMinutes) {
      intervalSelect.value = String(s.intervalMinutes);
    }
    if (s.consecutiveErrorAlertThreshold) {
      consecutiveErrorThresholdSelect.value = String(s.consecutiveErrorAlertThreshold);
    }
  })
  .catch(() => {});

triggerBrain.addEventListener("click", async () => {
  await fetch("/api/brain/trigger", { method: "POST" });
});

mitigateCongestion.addEventListener("click", async () => {
  await fetch("/api/logistics/mitigate", { method: "POST" });
});

loadOpsReport.addEventListener("click", async () => {
  const res = await fetch("/api/ops/report");
  const data = await res.json();
  opsReportEl.textContent = JSON.stringify(data, null, 2);
});
