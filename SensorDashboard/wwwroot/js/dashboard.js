(function () {
    'use strict';

    const HISTORY_LIMIT = 60;
    const TABLE_ROWS = 10;
    const STALE_AFTER_MS = 10000;
    const FRESHNESS_CHECK_MS = 2000;
    const RECONNECT_DELAY_MS = 5000;
    const HISTORY_ENDPOINT = '/Home/Recent';

    const hubUrl = document.getElementById('dashboard').dataset.hubUrl;

    const elements = {
        badge: document.getElementById('status-badge'),
        lastUpdated: document.getElementById('last-updated'),
        temp: document.getElementById('stat-temp'),
        tempRange: document.getElementById('stat-temp-range'),
        humidity: document.getElementById('stat-humidity'),
        avgTemp: document.getElementById('stat-avg-temp'),
        avgHumidity: document.getElementById('stat-avg-humidity'),
        windowTemp: document.getElementById('stat-window-temp'),
        windowHumidity: document.getElementById('stat-window-humidity'),
        tableBody: document.getElementById('readings-body')
    };

    const chart = new Chart(document.getElementById('history-chart'), {
        type: 'line',
        data: {
            labels: [],
            datasets: [
                {
                    label: 'Temperature',
                    data: [],
                    yAxisID: 'yTemp',
                    borderColor: '#f97316',
                    backgroundColor: 'rgba(249, 115, 22, 0.12)',
                    tension: 0.3,
                    pointRadius: 0,
                    fill: true
                },
                {
                    label: 'Humidity',
                    data: [],
                    yAxisID: 'yHum',
                    borderColor: '#0ea5e9',
                    backgroundColor: 'rgba(14, 165, 233, 0.08)',
                    tension: 0.3,
                    pointRadius: 0,
                    fill: true
                }
            ]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            animation: false,
            interaction: { mode: 'index', intersect: false },
            plugins: { legend: { labels: { usePointStyle: true } } },
            scales: {
                x: { ticks: { maxTicksLimit: 8 } },
                yTemp: {
                    type: 'linear',
                    position: 'left',
                    title: { display: true, text: 'Temperature (°C)' }
                },
                yHum: {
                    type: 'linear',
                    position: 'right',
                    min: 0,
                    max: 100,
                    grid: { drawOnChartArea: false },
                    title: { display: true, text: 'Humidity (%)' }
                }
            }
        }
    });

    // Newest reading first, capped at HISTORY_LIMIT entries.
    const readings = [];

    // EF Core/SQLite returns DateTime with an unspecified kind; treat it as UTC.
    function parseUtc(value) {
        return new Date(/([zZ]|[+-]\d{2}:?\d{2})$/.test(value) ? value : value + 'Z');
    }

    function setStatus(text, cssClass) {
        elements.badge.textContent = text;
        elements.badge.className = 'badge ' + cssClass;
    }

    function formatTime(date) {
        return date.toLocaleTimeString();
    }

    function render() {
        if (!readings.length) {
            return;
        }

        const latest = readings[0];
        const temps = readings.map(r => r.tempRead);
        const humidities = readings.map(r => r.humidity);
        const avg = values => values.reduce((sum, value) => sum + value, 0) / values.length;

        elements.temp.textContent = `${latest.tempRead.toFixed(1)}°${latest.unit}`;
        elements.tempRange.textContent =
            `${Math.min(...temps).toFixed(1)}° – ${Math.max(...temps).toFixed(1)}°`;
        elements.humidity.textContent = `${latest.humidity.toFixed(1)}%`;
        elements.avgTemp.textContent = `${avg(temps).toFixed(1)}°C`;
        elements.avgHumidity.textContent = `${avg(humidities).toFixed(1)}%`;
        elements.windowTemp.textContent = `Last ${readings.length} readings`;
        elements.windowHumidity.textContent = `Last ${readings.length} readings`;

        const ordered = [...readings].reverse();
        chart.data.labels = ordered.map(r => formatTime(parseUtc(r.readTime)));
        chart.data.datasets[0].data = ordered.map(r => r.tempRead);
        chart.data.datasets[1].data = ordered.map(r => r.humidity);
        chart.update('none');

        elements.tableBody.innerHTML = readings
            .slice(0, TABLE_ROWS)
            .map(r => `<tr>
                <td>${formatTime(parseUtc(r.readTime))}</td>
                <td>${r.tempRead.toFixed(1)} °${r.unit}</td>
                <td>${r.humidity.toFixed(1)} %</td>
            </tr>`)
            .join('');
    }

    function updateFreshness() {
        if (!readings.length) {
            setStatus('No data', 'text-bg-secondary');
            return;
        }

        const ageMs = Date.now() - parseUtc(readings[0].readTime).getTime();
        if (ageMs > STALE_AFTER_MS) {
            setStatus('Stale', 'text-bg-warning');
        } else {
            setStatus('Live', 'text-bg-success');
        }
    }

    async function loadHistory() {
        const response = await fetch(`${HISTORY_ENDPOINT}?limit=${HISTORY_LIMIT}`, {
            headers: { Accept: 'application/json' }
        });
        if (!response.ok) {
            throw new Error(`HTTP ${response.status}`);
        }

        const data = await response.json();
        readings.length = 0;
        readings.push(...data.slice(0, HISTORY_LIMIT));
    }

    const connection = new signalR.HubConnectionBuilder()
        .withUrl(hubUrl)
        .build();

    connection.on('ReadingReceived', reading => {
        readings.unshift(reading);
        if (readings.length > HISTORY_LIMIT) {
            readings.length = HISTORY_LIMIT;
        }

        render();
        elements.lastUpdated.textContent = `Updated ${formatTime(new Date())}`;
        setStatus('Live', 'text-bg-success');
    });

    connection.onclose(() => {
        setStatus('Disconnected', 'text-bg-danger');
        setTimeout(connect, RECONNECT_DELAY_MS);
    });

    async function connect() {
        try {
            await connection.start();
            // Refill after (re)connecting so no readings are missed during an outage.
            try {
                await loadHistory();
                render();
            } catch {
                // History is best-effort; live pushes still apply.
            }
            updateFreshness();
        } catch {
            setStatus('Disconnected', 'text-bg-danger');
            setTimeout(connect, RECONNECT_DELAY_MS);
        }
    }

    async function start() {
        try {
            await loadHistory();
            render();
        } catch {
            // The hub connection below drives the status badge.
        }

        setStatus('Connecting…', 'text-bg-secondary');
        await connect();

        setInterval(() => {
            if (connection.state === signalR.HubConnectionState.Connected) {
                updateFreshness();
            }
        }, FRESHNESS_CHECK_MS);
    }

    start();
})();
