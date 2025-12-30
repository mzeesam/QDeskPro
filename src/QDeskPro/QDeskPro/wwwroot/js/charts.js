// QDeskPro Charts - Chart.js Integration
// Initialize Chart.js defaults
if (typeof Chart !== 'undefined') {
    Chart.defaults.font.family = "'Inter', 'Roboto', sans-serif";
    Chart.defaults.color = '#666';
    Chart.defaults.plugins.tooltip.backgroundColor = 'rgba(0,0,0,0.8)';
    Chart.defaults.plugins.tooltip.cornerRadius = 8;
}

// Chart instance registry for proper cleanup
const chartRegistry = {};

// Helper function to safely destroy and recreate charts
function getOrCreateChart(canvasId, chartConfig) {
    const canvas = document.getElementById(canvasId);
    if (!canvas) {
        console.warn(`Canvas not found: ${canvasId}`);
        return null;
    }

    // Destroy existing chart on this canvas
    if (chartRegistry[canvasId]) {
        chartRegistry[canvasId].destroy();
        delete chartRegistry[canvasId];
    }

    const ctx = canvas.getContext('2d');
    const chart = new Chart(ctx, chartConfig);
    chartRegistry[canvasId] = chart;
    return chart;
}

// Dashboard Charts
window.QDeskCharts = {
    createSalesChart: function(canvasId, labels, salesData, expensesData) {
        return getOrCreateChart(canvasId, {
            type: 'bar',
            data: {
                labels: labels,
                datasets: [
                    {
                        label: 'Revenue',
                        data: salesData,
                        backgroundColor: 'rgba(25, 118, 210, 0.8)',
                        borderColor: '#1976D2',
                        borderWidth: 1,
                        borderRadius: 4,
                        order: 2
                    },
                    {
                        label: 'Expenses',
                        data: expensesData,
                        backgroundColor: 'rgba(244, 67, 54, 0.6)',
                        borderColor: '#F44336',
                        borderWidth: 2,
                        type: 'line',
                        fill: false,
                        tension: 0.4,
                        order: 1
                    }
                ]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                interaction: {
                    mode: 'index',
                    intersect: false,
                },
                plugins: {
                    legend: {
                        position: 'top',
                        labels: {
                            usePointStyle: true,
                            padding: 20
                        }
                    },
                    tooltip: {
                        callbacks: {
                            label: function(context) {
                                return context.dataset.label + ': KES ' +
                                    context.raw.toLocaleString();
                            }
                        }
                    }
                },
                scales: {
                    x: {
                        grid: { display: false }
                    },
                    y: {
                        beginAtZero: true,
                        ticks: {
                            callback: function(value) {
                                return 'KES ' + value.toLocaleString();
                            }
                        }
                    }
                }
            }
        });
    },

    createProfitGauge: function(canvasId, profitMargin) {
        // Determine color based on profit margin
        let gaugeColor = '#F44336'; // Red for < 20%
        if (profitMargin >= 40) gaugeColor = '#4CAF50'; // Green
        else if (profitMargin >= 20) gaugeColor = '#FF9800'; // Orange

        return getOrCreateChart(canvasId, {
            type: 'doughnut',
            data: {
                datasets: [{
                    data: [profitMargin, 100 - profitMargin],
                    backgroundColor: [gaugeColor, '#E0E0E0'],
                    borderWidth: 0,
                    circumference: 180,
                    rotation: 270,
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                cutout: '75%',
                plugins: {
                    legend: { display: false },
                    tooltip: { enabled: false }
                }
            }
        });
    },

    createProductPieChart: function(canvasId, labels, data) {
        return getOrCreateChart(canvasId, {
            type: 'pie',
            data: {
                labels: labels,
                datasets: [{
                    data: data,
                    backgroundColor: [
                        '#1976D2', '#4CAF50', '#FF9800',
                        '#9C27B0', '#00BCD4', '#795548',
                        '#E91E63', '#3F51B5', '#009688'
                    ],
                    borderWidth: 2,
                    borderColor: '#fff'
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                plugins: {
                    legend: {
                        position: 'right',
                        labels: { usePointStyle: true }
                    }
                }
            }
        });
    },

    destroyChart: function(canvasId) {
        if (chartRegistry[canvasId]) {
            chartRegistry[canvasId].destroy();
            delete chartRegistry[canvasId];
        }
    },

    destroyAllCharts: function() {
        Object.keys(chartRegistry).forEach(id => {
            if (chartRegistry[id]) {
                chartRegistry[id].destroy();
                delete chartRegistry[id];
            }
        });
    }
};

// Report Charts - More advanced charts for manager reports
window.QDeskReportCharts = {
    // Sales trend chart with revenue and expenses
    createSalesTrendChart: function(canvasId, labels, revenues, expenses) {
        return getOrCreateChart(canvasId, {
            type: 'bar',
            data: {
                labels: labels,
                datasets: [
                    {
                        label: 'Revenue',
                        data: revenues,
                        backgroundColor: 'rgba(76, 175, 80, 0.8)',
                        borderColor: '#4CAF50',
                        borderWidth: 1,
                        borderRadius: 6,
                        order: 2
                    },
                    {
                        label: 'Expenses',
                        data: expenses,
                        backgroundColor: 'rgba(244, 67, 54, 0.7)',
                        borderColor: '#F44336',
                        borderWidth: 2,
                        type: 'line',
                        fill: false,
                        tension: 0.3,
                        pointRadius: 4,
                        pointBackgroundColor: '#F44336',
                        order: 1
                    }
                ]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                interaction: {
                    mode: 'index',
                    intersect: false
                },
                plugins: {
                    legend: {
                        position: 'top',
                        labels: {
                            usePointStyle: true,
                            padding: 15,
                            font: { size: 12 }
                        }
                    },
                    tooltip: {
                        backgroundColor: 'rgba(30, 41, 59, 0.95)',
                        padding: 12,
                        titleFont: { size: 13 },
                        bodyFont: { size: 12 },
                        callbacks: {
                            label: function(context) {
                                return context.dataset.label + ': KES ' + context.raw.toLocaleString();
                            }
                        }
                    }
                },
                scales: {
                    x: {
                        grid: { display: false }
                    },
                    y: {
                        beginAtZero: true,
                        grid: { color: 'rgba(0,0,0,0.05)' },
                        ticks: {
                            callback: function(value) {
                                if (value >= 1000000) return 'KES ' + (value / 1000000).toFixed(1) + 'M';
                                if (value >= 1000) return 'KES ' + (value / 1000).toFixed(0) + 'K';
                                return 'KES ' + value.toLocaleString();
                            }
                        }
                    }
                }
            }
        });
    },

    // Product distribution pie/doughnut chart
    createProductPieChart: function(canvasId, labels, data) {
        return getOrCreateChart(canvasId, {
            type: 'doughnut',
            data: {
                labels: labels,
                datasets: [{
                    data: data,
                    backgroundColor: [
                        '#1976D2', '#4CAF50', '#FF9800',
                        '#9C27B0', '#00BCD4', '#795548',
                        '#E91E63', '#3F51B5', '#009688'
                    ],
                    borderWidth: 3,
                    borderColor: '#fff',
                    hoverOffset: 8
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                cutout: '50%',
                plugins: {
                    legend: {
                        position: 'right',
                        labels: {
                            usePointStyle: true,
                            padding: 15,
                            font: { size: 11 }
                        }
                    },
                    tooltip: {
                        backgroundColor: 'rgba(30, 41, 59, 0.95)',
                        padding: 12,
                        callbacks: {
                            label: function(context) {
                                const total = context.dataset.data.reduce((a, b) => a + b, 0);
                                const percentage = ((context.raw / total) * 100).toFixed(1);
                                return context.label + ': KES ' + context.raw.toLocaleString() + ' (' + percentage + '%)';
                            }
                        }
                    }
                }
            }
        });
    },

    // Expense breakdown doughnut chart
    createExpenseBreakdownChart: function(canvasId, categories, values) {
        return getOrCreateChart(canvasId, {
            type: 'doughnut',
            data: {
                labels: categories,
                datasets: [{
                    data: values,
                    backgroundColor: [
                        '#FF9800', // Commission - Orange
                        '#00BCD4', // Loaders Fee - Cyan
                        '#4CAF50', // Land Rate - Green
                        '#795548'  // Manual - Brown
                    ],
                    borderWidth: 3,
                    borderColor: '#fff',
                    hoverOffset: 10
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                cutout: '60%',
                plugins: {
                    legend: {
                        position: 'bottom',
                        labels: {
                            usePointStyle: true,
                            padding: 15,
                            font: { size: 11 }
                        }
                    },
                    tooltip: {
                        backgroundColor: 'rgba(30, 41, 59, 0.95)',
                        padding: 12,
                        callbacks: {
                            label: function(context) {
                                const total = context.dataset.data.reduce((a, b) => a + b, 0);
                                const percentage = total > 0 ? ((context.raw / total) * 100).toFixed(1) : 0;
                                return context.label + ': KES ' + context.raw.toLocaleString() + ' (' + percentage + '%)';
                            }
                        }
                    }
                }
            }
        });
    },

    // Expense trend line chart
    createExpenseTrendChart: function(canvasId, labels, data) {
        return getOrCreateChart(canvasId, {
            type: 'line',
            data: {
                labels: labels,
                datasets: [{
                    label: 'Daily Expenses',
                    data: data,
                    backgroundColor: 'rgba(244, 67, 54, 0.1)',
                    borderColor: '#F44336',
                    borderWidth: 2,
                    fill: true,
                    tension: 0.4,
                    pointRadius: 4,
                    pointBackgroundColor: '#F44336',
                    pointHoverRadius: 6
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                interaction: {
                    mode: 'index',
                    intersect: false
                },
                plugins: {
                    legend: { display: false },
                    tooltip: {
                        backgroundColor: 'rgba(30, 41, 59, 0.95)',
                        padding: 12,
                        callbacks: {
                            label: function(context) {
                                return 'Expenses: KES ' + context.raw.toLocaleString();
                            }
                        }
                    }
                },
                scales: {
                    x: {
                        grid: { display: false }
                    },
                    y: {
                        beginAtZero: true,
                        grid: { color: 'rgba(0,0,0,0.05)' },
                        ticks: {
                            callback: function(value) {
                                return 'KES ' + value.toLocaleString();
                            }
                        }
                    }
                }
            }
        });
    },

    // Fuel usage stacked bar chart
    createFuelUsageChart: function(canvasId, labels, machineUsage, loaderUsage) {
        return getOrCreateChart(canvasId, {
            type: 'bar',
            data: {
                labels: labels,
                datasets: [
                    {
                        label: 'Machines',
                        data: machineUsage,
                        backgroundColor: 'rgba(255, 152, 0, 0.8)',
                        borderColor: '#FF9800',
                        borderWidth: 1,
                        borderRadius: 4
                    },
                    {
                        label: 'Wheel Loaders',
                        data: loaderUsage,
                        backgroundColor: 'rgba(233, 30, 99, 0.8)',
                        borderColor: '#E91E63',
                        borderWidth: 1,
                        borderRadius: 4
                    }
                ]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                interaction: {
                    mode: 'index',
                    intersect: false
                },
                plugins: {
                    legend: {
                        position: 'top',
                        labels: {
                            usePointStyle: true,
                            padding: 15
                        }
                    },
                    tooltip: {
                        backgroundColor: 'rgba(30, 41, 59, 0.95)',
                        padding: 12,
                        callbacks: {
                            label: function(context) {
                                return context.dataset.label + ': ' + context.raw.toFixed(1) + ' L';
                            }
                        }
                    }
                },
                scales: {
                    x: {
                        stacked: true,
                        grid: { display: false }
                    },
                    y: {
                        stacked: true,
                        beginAtZero: true,
                        grid: { color: 'rgba(0,0,0,0.05)' },
                        ticks: {
                            callback: function(value) {
                                return value + ' L';
                            }
                        }
                    }
                }
            }
        });
    },

    // Banking trend bar chart
    createBankingTrendChart: function(canvasId, labels, amounts) {
        return getOrCreateChart(canvasId, {
            type: 'bar',
            data: {
                labels: labels,
                datasets: [{
                    label: 'Amount Banked',
                    data: amounts,
                    backgroundColor: 'rgba(25, 118, 210, 0.8)',
                    borderColor: '#1976D2',
                    borderWidth: 1,
                    borderRadius: 6,
                    hoverBackgroundColor: 'rgba(25, 118, 210, 1)'
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                plugins: {
                    legend: { display: false },
                    tooltip: {
                        backgroundColor: 'rgba(30, 41, 59, 0.95)',
                        padding: 12,
                        callbacks: {
                            label: function(context) {
                                return 'Banked: KES ' + context.raw.toLocaleString();
                            }
                        }
                    }
                },
                scales: {
                    x: {
                        grid: { display: false }
                    },
                    y: {
                        beginAtZero: true,
                        grid: { color: 'rgba(0,0,0,0.05)' },
                        ticks: {
                            callback: function(value) {
                                if (value >= 1000000) return 'KES ' + (value / 1000000).toFixed(1) + 'M';
                                if (value >= 1000) return 'KES ' + (value / 1000).toFixed(0) + 'K';
                                return 'KES ' + value.toLocaleString();
                            }
                        }
                    }
                }
            }
        });
    },

    // Destroy all report charts
    destroyAll: function() {
        Object.keys(chartRegistry).forEach(id => {
            if (chartRegistry[id]) {
                chartRegistry[id].destroy();
                delete chartRegistry[id];
            }
        });
    }
};

// File download helper
window.downloadFile = function(fileName, mimeType, base64Content) {
    const link = document.createElement('a');
    link.href = 'data:' + mimeType + ';base64,' + base64Content;
    link.download = fileName;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
};

// ROI Analysis Charts
window.QDeskROICharts = {
    // Recovery gauge (doughnut chart showing investment recovery progress)
    createRecoveryGauge: function(canvasId, recoveryPercent) {
        // Clamp percentage between 0 and 100
        const percent = Math.min(100, Math.max(0, recoveryPercent));

        // Determine color based on recovery percentage
        let gaugeColor = '#F44336'; // Red for < 50%
        if (percent >= 100) gaugeColor = '#4CAF50'; // Green - recovered
        else if (percent >= 50) gaugeColor = '#FF9800'; // Orange

        return getOrCreateChart(canvasId, {
            type: 'doughnut',
            data: {
                datasets: [{
                    data: [percent, 100 - percent],
                    backgroundColor: [gaugeColor, '#E0E0E0'],
                    borderWidth: 0,
                    circumference: 180,
                    rotation: 270,
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                cutout: '75%',
                plugins: {
                    legend: { display: false },
                    tooltip: { enabled: false }
                }
            }
        });
    },

    // Cumulative profit vs investment line chart
    createCumulativeProfitChart: function(canvasId, labels, profitData, investmentLine) {
        return getOrCreateChart(canvasId, {
            type: 'line',
            data: {
                labels: labels,
                datasets: [
                    {
                        label: 'Investment',
                        data: investmentLine,
                        borderColor: '#F44336',
                        borderWidth: 2,
                        borderDash: [5, 5],
                        fill: false,
                        pointRadius: 0,
                        tension: 0,
                        order: 1
                    },
                    {
                        label: 'Cumulative Profit',
                        data: profitData,
                        backgroundColor: 'rgba(76, 175, 80, 0.15)',
                        borderColor: '#4CAF50',
                        borderWidth: 3,
                        fill: true,
                        tension: 0.3,
                        pointRadius: 4,
                        pointBackgroundColor: '#4CAF50',
                        pointHoverRadius: 6,
                        order: 2
                    }
                ]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                interaction: {
                    mode: 'index',
                    intersect: false
                },
                plugins: {
                    legend: {
                        position: 'top',
                        labels: {
                            usePointStyle: true,
                            padding: 15,
                            font: { size: 12 }
                        }
                    },
                    tooltip: {
                        backgroundColor: 'rgba(30, 41, 59, 0.95)',
                        padding: 12,
                        titleFont: { size: 13 },
                        bodyFont: { size: 12 },
                        callbacks: {
                            label: function(context) {
                                return context.dataset.label + ': KES ' + context.raw.toLocaleString();
                            }
                        }
                    },
                    annotation: {
                        annotations: {
                            breakEvenLine: {
                                type: 'line',
                                yMin: investmentLine[0],
                                yMax: investmentLine[0],
                                borderColor: '#F44336',
                                borderWidth: 2,
                                borderDash: [5, 5]
                            }
                        }
                    }
                },
                scales: {
                    x: {
                        grid: { display: false }
                    },
                    y: {
                        beginAtZero: false,
                        grid: { color: 'rgba(0,0,0,0.05)' },
                        ticks: {
                            callback: function(value) {
                                if (Math.abs(value) >= 1000000) return 'KES ' + (value / 1000000).toFixed(1) + 'M';
                                if (Math.abs(value) >= 1000) return 'KES ' + (value / 1000).toFixed(0) + 'K';
                                return 'KES ' + value.toLocaleString();
                            }
                        }
                    }
                }
            }
        });
    },

    // Monthly ROI trend chart
    createROITrendChart: function(canvasId, labels, roiData) {
        return getOrCreateChart(canvasId, {
            type: 'line',
            data: {
                labels: labels,
                datasets: [{
                    label: 'ROI %',
                    data: roiData,
                    backgroundColor: 'rgba(156, 39, 176, 0.1)',
                    borderColor: '#9C27B0',
                    borderWidth: 2,
                    fill: true,
                    tension: 0.4,
                    pointRadius: 4,
                    pointBackgroundColor: '#9C27B0',
                    pointHoverRadius: 6
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                interaction: {
                    mode: 'index',
                    intersect: false
                },
                plugins: {
                    legend: { display: false },
                    tooltip: {
                        backgroundColor: 'rgba(30, 41, 59, 0.95)',
                        padding: 12,
                        callbacks: {
                            label: function(context) {
                                return 'ROI: ' + context.raw.toFixed(1) + '%';
                            }
                        }
                    }
                },
                scales: {
                    x: {
                        grid: { display: false }
                    },
                    y: {
                        beginAtZero: true,
                        grid: { color: 'rgba(0,0,0,0.05)' },
                        ticks: {
                            callback: function(value) {
                                return value + '%';
                            }
                        }
                    }
                }
            }
        });
    },

    // Destroy ROI charts
    destroyAll: function() {
        ['recoveryGauge', 'cumulativeProfitChart', 'roiTrendChart'].forEach(id => {
            if (chartRegistry[id]) {
                chartRegistry[id].destroy();
                delete chartRegistry[id];
            }
        });
    }
};

// Utility to destroy charts when navigating away
if (typeof Blazor !== 'undefined') {
    Blazor.addEventListener('enhancedload', function() {
        // Cleanup charts on Blazor navigation
        if (window.QDeskCharts) {
            window.QDeskCharts.destroyAllCharts();
        }
        if (window.QDeskReportCharts) {
            window.QDeskReportCharts.destroyAll();
        }
        if (window.QDeskROICharts) {
            window.QDeskROICharts.destroyAll();
        }
    });
}
