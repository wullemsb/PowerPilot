const charts = new Map();

export function render(canvas, points, isStale) {
    const labels = points.map(point => point.timestamp);
    const consumption = points.map(point => point.consumption);
    const production = points.map(point => point.production);
    const peakValue = Math.max(...consumption);
    const peakIndex = consumption.indexOf(peakValue);

    const data = {
        labels,
        datasets: [
            {
                label: "Consumption",
                data: consumption,
                borderColor: "#ff9000",
                backgroundColor: "rgba(255, 144, 0, 0.12)",
                pointBackgroundColor: "#ff9000",
                pointRadius: 2,
                tension: 0.25,
                fill: true
            },
            {
                label: "Solar production",
                data: production,
                borderColor: "#0ba631",
                backgroundColor: "rgba(11, 166, 49, 0.12)",
                pointBackgroundColor: "#0ba631",
                pointRadius: 2,
                tension: 0.25,
                fill: true
            },
            {
                label: "Peak consumption",
                data: points.map((point, index) => index === peakIndex ? point.consumption : null),
                borderColor: "#fc440f",
                backgroundColor: "#fc440f",
                pointRadius: 6,
                pointHoverRadius: 8,
                showLine: false
            }
        ]
    };

    const options = {
        responsive: true,
        maintainAspectRatio: false,
        animation: { duration: 350 },
        interaction: { mode: "index", intersect: false },
        plugins: {
            legend: { position: "bottom", labels: { usePointStyle: true, boxWidth: 8 } },
            tooltip: {
                callbacks: {
                    label(context) {
                        const value = Math.abs(context.parsed.y).toFixed(1);
                        return `${context.dataset.label}: ${value} kW`;
                    },
                    afterBody(items) {
                        if (items.some(item => item.dataset.label === "Peak consumption")) {
                            const item = items.find(item => item.dataset.label === "Peak consumption");
                            return `Grid import: ${Math.max(0, consumption[item.dataIndex] - Math.abs(production[item.dataIndex])).toFixed(1)} kW`;
                        }
                        return [];
                    }
                }
            }
        },
        scales: {
            x: { grid: { display: false }, ticks: { maxTicksLimit: 8 } },
            y: {
                title: { display: true, text: "kW" },
                ticks: { callback: value => `${Math.abs(value).toFixed(1)}` },
                grid: { color: context => context.tick.value === 0 ? "#28293e" : "#ebebf1" }
            }
        }
    };

    const existingChart = charts.get(canvas);
    if (existingChart) {
        existingChart.data = data;
        existingChart.options = options;
        existingChart.update();
        canvas.closest(".energy-trend")?.classList.toggle("is-stale", isStale);
        return;
    }

    charts.set(canvas, new Chart(canvas, { type: "line", data, options }));
    canvas.closest(".energy-trend")?.classList.toggle("is-stale", isStale);
}

export function renderNet(canvas, points) {
    const data = {
        labels: points.map(point => point.timestamp),
        datasets: [{
            label: "Net grid flow",
            data: points.map(point => point.consumption - point.production),
            borderColor: "#28293e",
            backgroundColor: "rgba(40, 41, 62, 0.12)",
            pointRadius: 1.5,
            tension: 0.25,
            fill: true
        }]
    };
    renderSimple(canvas, data, "kW", "Net grid flow");
}

export function renderComparison(canvas, points) {
    const data = {
        labels: points.map(point => point.timestamp),
        datasets: [
            { label: "Consumption", data: points.map(point => point.consumption), backgroundColor: "#ff9000", borderRadius: 3 },
            { label: "Solar production", data: points.map(point => point.production), backgroundColor: "#0ba631", borderRadius: 3 }
        ]
    };
    renderSimple(canvas, data, "kW", "Power");
}

function renderSimple(canvas, data, yTitle, tooltipTitle) {
    const options = {
        responsive: true,
        maintainAspectRatio: false,
        animation: { duration: 350 },
        interaction: { mode: "index", intersect: false },
        plugins: {
            legend: { position: "bottom", labels: { usePointStyle: true, boxWidth: 8 } },
            tooltip: { callbacks: { label: context => `${context.dataset.label}: ${Math.abs(context.parsed.y).toFixed(1)} kW` } }
        },
        scales: {
            x: { grid: { display: false }, ticks: { maxTicksLimit: 10, maxRotation: 0 } },
            y: { title: { display: true, text: yTitle }, ticks: { callback: value => `${Math.abs(value).toFixed(1)}` } }
        }
    };
    const existingChart = charts.get(canvas);
    if (existingChart) {
        existingChart.data = data;
        existingChart.options = options;
        existingChart.update();
    }
    else {
        charts.set(canvas, new Chart(canvas, { type: canvas.dataset.chartType ?? "line", data, options }));
    }
}

export function dispose(...canvases) {
    for (const canvas of canvases) {
        const chart = charts.get(canvas);
        chart?.destroy();
        charts.delete(canvas);
    }
}