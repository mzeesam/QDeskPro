// QDeskPro Charts - Chart.js Integration
// Initialize Chart.js defaults
if (typeof Chart !== 'undefined') {
    Chart.defaults.font.family = "'Inter', 'Roboto', sans-serif";
    Chart.defaults.color = '#666';
    Chart.defaults.plugins.tooltip.backgroundColor = 'rgba(0,0,0,0.8)';
    Chart.defaults.plugins.tooltip.cornerRadius = 8;
}

// Sales Performance Charts
window.QDeskCharts = {
    salesPerformanceChart: null,
    profitGaugeChart: null,

    createSalesChart: function(canvasId, labels, salesData, expensesData) {
        const canvas = document.getElementById(canvasId);
        if (!canvas) return;
        
        const ctx = canvas.getContext('2d');

        if (this.salesPerformanceChart) {
            this.salesPerformanceChart.destroy();
        }

        this.salesPerformanceChart = new Chart(ctx, {
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
        const canvas = document.getElementById(canvasId);
        if (!canvas) return;
        
        const ctx = canvas.getContext('2d');

        if (this.profitGaugeChart) {
            this.profitGaugeChart.destroy();
        }

        // Determine color based on profit margin
        let gaugeColor = '#F44336'; // Red for < 20%
        if (profitMargin >= 40) gaugeColor = '#4CAF50'; // Green
        else if (profitMargin >= 20) gaugeColor = '#FF9800'; // Orange

        this.profitGaugeChart = new Chart(ctx, {
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
        const canvas = document.getElementById(canvasId);
        if (!canvas) return null;
        
        const ctx = canvas.getContext('2d');

        return new Chart(ctx, {
            type: 'pie',
            data: {
                labels: labels,
                datasets: [{
                    data: data,
                    backgroundColor: [
                        '#1976D2', '#4CAF50', '#FF9800',
                        '#9C27B0', '#00BCD4', '#795548'
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

    destroyChart: function(chartRef) {
        if (chartRef) chartRef.destroy();
    }
};
