/**
 * QDeskPro AI Chat Charts
 * Inline chart rendering for AI chat responses
 */
window.QDeskAIChatCharts = {
    chartInstances: {},

    // Color palette for charts
    colors: [
        '#1976D2', '#4CAF50', '#FF9800', '#9C27B0',
        '#00BCD4', '#795548', '#E91E63', '#3F51B5',
        '#009688', '#FF5722', '#607D8B', '#8BC34A'
    ],

    /**
     * Create a chart in the chat
     */
    createChart: function (canvasId, type, labels, datasets, options) {
        // Destroy existing chart if any
        this.destroyChart(canvasId);

        const canvas = document.getElementById(canvasId);
        if (!canvas) {
            console.warn(`Canvas not found: ${canvasId}`);
            return null;
        }

        const ctx = canvas.getContext('2d');
        const colors = this.colors;

        // Process datasets with default colors
        const processedDatasets = datasets.map((ds, i) => {
            const isPieOrDoughnut = type === 'pie' || type === 'doughnut';

            return {
                label: ds.label || `Dataset ${i + 1}`,
                data: ds.data,
                backgroundColor: isPieOrDoughnut
                    ? colors.slice(0, ds.data.length).map(c => c + 'CC')
                    : ds.backgroundColor || colors[i % colors.length] + 'CC',
                borderColor: isPieOrDoughnut
                    ? colors.slice(0, ds.data.length)
                    : ds.borderColor || colors[i % colors.length],
                borderWidth: 2,
                borderRadius: type === 'bar' ? 4 : 0
            };
        });

        const isPieChart = type === 'pie' || type === 'doughnut';

        this.chartInstances[canvasId] = new Chart(ctx, {
            type: type,
            data: {
                labels: labels,
                datasets: processedDatasets
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                plugins: {
                    legend: {
                        display: options?.showLegend ?? isPieChart,
                        position: isPieChart ? 'right' : 'bottom',
                        labels: {
                            usePointStyle: true,
                            padding: 10,
                            font: { size: 11 }
                        }
                    },
                    tooltip: {
                        backgroundColor: 'rgba(0,0,0,0.8)',
                        padding: 10,
                        cornerRadius: 4,
                        titleFont: { size: 12 },
                        bodyFont: { size: 11 },
                        callbacks: {
                            label: function (context) {
                                let value = context.raw;
                                if (options?.yAxisFormat === 'currency') {
                                    value = 'KES ' + value.toLocaleString('en-KE');
                                } else {
                                    value = value.toLocaleString('en-KE');
                                }
                                return isPieChart
                                    ? `${context.label}: ${value}`
                                    : `${context.dataset.label}: ${value}`;
                            }
                        }
                    }
                },
                scales: isPieChart ? {} : {
                    x: {
                        grid: { display: false },
                        ticks: { font: { size: 10 } }
                    },
                    y: {
                        beginAtZero: true,
                        grid: { color: 'rgba(0,0,0,0.05)' },
                        ticks: {
                            font: { size: 10 },
                            callback: function (value) {
                                if (options?.yAxisFormat === 'currency') {
                                    if (value >= 1000000) {
                                        return 'KES ' + (value / 1000000).toFixed(1) + 'M';
                                    } else if (value >= 1000) {
                                        return 'KES ' + (value / 1000).toFixed(0) + 'K';
                                    }
                                    return 'KES ' + value.toLocaleString('en-KE');
                                }
                                return value.toLocaleString('en-KE');
                            }
                        }
                    }
                }
            }
        });

        return this.chartInstances[canvasId];
    },

    /**
     * Export chart as PNG image
     */
    exportChartAsImage: function (canvasId, filename) {
        const chart = this.chartInstances[canvasId];
        if (!chart) {
            console.warn(`Chart not found: ${canvasId}`);
            return;
        }

        const canvas = document.getElementById(canvasId);
        if (!canvas) return;

        // Create a temporary link to download
        const link = document.createElement('a');
        link.download = `${filename || 'chart'}_${new Date().toISOString().split('T')[0]}.png`;
        link.href = canvas.toDataURL('image/png', 1.0);
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
    },

    /**
     * Destroy chart instance
     */
    destroyChart: function (canvasId) {
        if (this.chartInstances[canvasId]) {
            this.chartInstances[canvasId].destroy();
            delete this.chartInstances[canvasId];
        }
    },

    /**
     * Update chart data
     */
    updateChart: function (canvasId, labels, datasets) {
        const chart = this.chartInstances[canvasId];
        if (!chart) return;

        chart.data.labels = labels;
        chart.data.datasets.forEach((ds, i) => {
            if (datasets[i]) {
                ds.data = datasets[i].data;
            }
        });
        chart.update();
    }
};

/**
 * Download file utility for CSV export
 * Note: Parameters match standard order used across the app (fileName, mimeType, base64Content)
 */
window.downloadFile = function (filename, mimeType, base64Content) {
    const link = document.createElement('a');
    link.href = `data:${mimeType};base64,${base64Content}`;
    link.download = filename;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
};
