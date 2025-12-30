/**
 * QDeskPro Data Analytics Charts
 * Advanced Chart.js visualizations for cost analytics dashboard
 */

window.QDeskDataAnalyticsCharts = {
    chartInstances: {},

    /**
     * 1. Cost Breakdown Stacked Bar Chart
     * Shows daily/weekly cost breakdown by category
     *
     * @param {string} canvasId - Canvas element ID
     * @param {string[]} labels - X-axis labels (dates)
     * @param {number[]} manualData - Manual expenses data
     * @param {number[]} commissionData - Commission expenses data
     * @param {number[]} loadersData - Loaders fee data
     * @param {number[]} landRateData - Land rate fee data
     * @param {number[]} fuelData - Fuel cost data
     */
    createCostBreakdownChart: function(canvasId, labels, manualData, commissionData, loadersData, landRateData, fuelData) {
        this.destroyChart(canvasId);

        const ctx = document.getElementById(canvasId);
        if (!ctx) {
            console.error(`Canvas element with ID '${canvasId}' not found`);
            return null;
        }

        this.chartInstances[canvasId] = new Chart(ctx.getContext('2d'), {
            type: 'bar',
            data: {
                labels: labels,
                datasets: [
                    {
                        label: 'Manual Expenses',
                        data: manualData,
                        backgroundColor: '#9C27B0',
                        borderColor: '#9C27B0',
                        borderWidth: 1,
                        stack: 'costs'
                    },
                    {
                        label: 'Commission',
                        data: commissionData,
                        backgroundColor: '#FF9800',
                        borderColor: '#FF9800',
                        borderWidth: 1,
                        stack: 'costs'
                    },
                    {
                        label: 'Loaders Fee',
                        data: loadersData,
                        backgroundColor: '#2196F3',
                        borderColor: '#2196F3',
                        borderWidth: 1,
                        stack: 'costs'
                    },
                    {
                        label: 'Land Rate',
                        data: landRateData,
                        backgroundColor: '#4CAF50',
                        borderColor: '#4CAF50',
                        borderWidth: 1,
                        stack: 'costs'
                    },
                    {
                        label: 'Fuel Cost',
                        data: fuelData,
                        backgroundColor: '#F44336',
                        borderColor: '#F44336',
                        borderWidth: 1,
                        stack: 'costs'
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
                        position: 'bottom',
                        labels: {
                            usePointStyle: true,
                            padding: 15,
                            font: {
                                size: 12
                            }
                        }
                    },
                    tooltip: {
                        backgroundColor: 'rgba(0, 0, 0, 0.8)',
                        padding: 12,
                        titleFont: {
                            size: 14,
                            weight: 'bold'
                        },
                        bodyFont: {
                            size: 13
                        },
                        callbacks: {
                            label: function(context) {
                                return context.dataset.label + ': KES ' + context.raw.toLocaleString('en-KE', { minimumFractionDigits: 0, maximumFractionDigits: 0 });
                            },
                            footer: function(tooltipItems) {
                                let total = 0;
                                tooltipItems.forEach(function(item) {
                                    total += item.raw;
                                });
                                return 'Total: KES ' + total.toLocaleString('en-KE', { minimumFractionDigits: 0, maximumFractionDigits: 0 });
                            }
                        }
                    }
                },
                scales: {
                    x: {
                        stacked: true,
                        grid: {
                            display: false
                        },
                        ticks: {
                            font: {
                                size: 11
                            }
                        }
                    },
                    y: {
                        stacked: true,
                        beginAtZero: true,
                        grid: {
                            color: 'rgba(0, 0, 0, 0.05)'
                        },
                        ticks: {
                            callback: function(value) {
                                return 'KES ' + value.toLocaleString('en-KE', { minimumFractionDigits: 0, maximumFractionDigits: 0 });
                            },
                            font: {
                                size: 11
                            }
                        }
                    }
                }
            }
        });

        return this.chartInstances[canvasId];
    },

    /**
     * 2. Fuel Efficiency Dual-Axis Chart
     * Line chart for efficiency (pcs/L) + bar chart for daily quantity
     *
     * @param {string} canvasId - Canvas element ID
     * @param {string[]} labels - X-axis labels (dates)
     * @param {number[]} efficiencyData - Fuel efficiency data (pieces per liter)
     * @param {number[]} quantityData - Daily quantity data
     */
    createFuelEfficiencyChart: function(canvasId, labels, efficiencyData, quantityData) {
        this.destroyChart(canvasId);

        const ctx = document.getElementById(canvasId);
        if (!ctx) {
            console.error(`Canvas element with ID '${canvasId}' not found`);
            return null;
        }

        this.chartInstances[canvasId] = new Chart(ctx.getContext('2d'), {
            type: 'line',
            data: {
                labels: labels,
                datasets: [
                    {
                        label: 'Fuel Efficiency (pcs/L)',
                        data: efficiencyData,
                        borderColor: '#4CAF50',
                        backgroundColor: 'rgba(76, 175, 80, 0.1)',
                        yAxisID: 'y',
                        tension: 0.4,
                        fill: true,
                        borderWidth: 3,
                        pointRadius: 4,
                        pointHoverRadius: 6,
                        pointBackgroundColor: '#4CAF50',
                        pointBorderColor: '#fff',
                        pointBorderWidth: 2
                    },
                    {
                        label: 'Daily Quantity',
                        data: quantityData,
                        type: 'bar',
                        backgroundColor: 'rgba(33, 150, 243, 0.3)',
                        borderColor: '#2196F3',
                        borderWidth: 1,
                        yAxisID: 'y1'
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
                            font: {
                                size: 12
                            }
                        }
                    },
                    tooltip: {
                        backgroundColor: 'rgba(0, 0, 0, 0.8)',
                        padding: 12,
                        titleFont: {
                            size: 14,
                            weight: 'bold'
                        },
                        bodyFont: {
                            size: 13
                        },
                        callbacks: {
                            label: function(context) {
                                if (context.dataset.label === 'Fuel Efficiency (pcs/L)') {
                                    return context.dataset.label + ': ' + context.raw.toFixed(2) + ' pcs/L';
                                } else {
                                    return context.dataset.label + ': ' + context.raw.toLocaleString('en-KE', { minimumFractionDigits: 0, maximumFractionDigits: 0 }) + ' pieces';
                                }
                            }
                        }
                    }
                },
                scales: {
                    x: {
                        grid: {
                            display: false
                        },
                        ticks: {
                            font: {
                                size: 11
                            }
                        }
                    },
                    y: {
                        type: 'linear',
                        display: true,
                        position: 'left',
                        title: {
                            display: true,
                            text: 'Efficiency (pcs/L)',
                            font: {
                                size: 12,
                                weight: 'bold'
                            },
                            color: '#4CAF50'
                        },
                        beginAtZero: true,
                        grid: {
                            color: 'rgba(76, 175, 80, 0.1)'
                        },
                        ticks: {
                            callback: function(value) {
                                return value.toFixed(1);
                            },
                            font: {
                                size: 11
                            },
                            color: '#4CAF50'
                        }
                    },
                    y1: {
                        type: 'linear',
                        display: true,
                        position: 'right',
                        title: {
                            display: true,
                            text: 'Quantity (pieces)',
                            font: {
                                size: 12,
                                weight: 'bold'
                            },
                            color: '#2196F3'
                        },
                        grid: {
                            drawOnChartArea: false
                        },
                        beginAtZero: true,
                        ticks: {
                            callback: function(value) {
                                return value.toLocaleString('en-KE', { minimumFractionDigits: 0, maximumFractionDigits: 0 });
                            },
                            font: {
                                size: 11
                            },
                            color: '#2196F3'
                        }
                    }
                }
            }
        });

        return this.chartInstances[canvasId];
    },

    /**
     * 3. Product Profitability Bubble Chart
     * X-axis: Revenue, Y-axis: Margin %, Bubble size: Quantity
     *
     * @param {string} canvasId - Canvas element ID
     * @param {Array<{label: string, x: number, y: number, r: number}>} products - Product data
     */
    createProfitabilityBubbleChart: function(canvasId, products) {
        this.destroyChart(canvasId);

        const ctx = document.getElementById(canvasId);
        if (!ctx) {
            console.error(`Canvas element with ID '${canvasId}' not found`);
            return null;
        }

        const colors = [
            '#1976D2', '#4CAF50', '#FF9800', '#9C27B0',
            '#00BCD4', '#795548', '#E91E63', '#3F51B5'
        ];

        const datasets = products.map((p, i) => ({
            label: p.label,
            data: [{
                x: p.x,
                y: p.y,
                r: Math.sqrt(p.r) / 3  // Scale bubble radius for better visualization
            }],
            backgroundColor: colors[i % colors.length] + 'CC',  // Add transparency
            borderColor: colors[i % colors.length],
            borderWidth: 2
        }));

        this.chartInstances[canvasId] = new Chart(ctx.getContext('2d'), {
            type: 'bubble',
            data: {
                datasets: datasets
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                plugins: {
                    legend: {
                        position: 'right',
                        labels: {
                            usePointStyle: true,
                            padding: 12,
                            font: {
                                size: 11
                            }
                        }
                    },
                    tooltip: {
                        backgroundColor: 'rgba(0, 0, 0, 0.8)',
                        padding: 12,
                        titleFont: {
                            size: 14,
                            weight: 'bold'
                        },
                        bodyFont: {
                            size: 13
                        },
                        callbacks: {
                            label: function(context) {
                                const p = context.raw;
                                const actualQuantity = (p.r * p.r * 9).toFixed(0);  // Reverse the scaling
                                return [
                                    'Revenue: KES ' + p.x.toLocaleString('en-KE', { minimumFractionDigits: 0, maximumFractionDigits: 0 }),
                                    'Margin: ' + p.y.toFixed(1) + '%',
                                    'Quantity: ' + actualQuantity + ' pieces'
                                ];
                            }
                        }
                    }
                },
                scales: {
                    x: {
                        title: {
                            display: true,
                            text: 'Revenue (KES)',
                            font: {
                                size: 12,
                                weight: 'bold'
                            }
                        },
                        grid: {
                            color: 'rgba(0, 0, 0, 0.05)'
                        },
                        ticks: {
                            callback: function(value) {
                                return 'KES ' + value.toLocaleString('en-KE', { minimumFractionDigits: 0, maximumFractionDigits: 0 });
                            },
                            font: {
                                size: 11
                            }
                        }
                    },
                    y: {
                        title: {
                            display: true,
                            text: 'Profit Margin (%)',
                            font: {
                                size: 12,
                                weight: 'bold'
                            }
                        },
                        grid: {
                            color: 'rgba(0, 0, 0, 0.05)'
                        },
                        ticks: {
                            callback: function(value) {
                                return value.toFixed(0) + '%';
                            },
                            font: {
                                size: 11
                            }
                        }
                    }
                }
            }
        });

        return this.chartInstances[canvasId];
    },

    /**
     * 4. Cash Flow Waterfall Chart
     * Shows cash flow from opening to closing balance
     *
     * @param {string} canvasId - Canvas element ID
     * @param {string[]} labels - Step labels
     * @param {number[]} values - Step values
     * @param {string[]} colors - Bar colors based on type
     */
    createCashFlowWaterfallChart: function(canvasId, labels, values, colors) {
        this.destroyChart(canvasId);

        const ctx = document.getElementById(canvasId);
        if (!ctx) {
            console.error(`Canvas element with ID '${canvasId}' not found`);
            return null;
        }

        this.chartInstances[canvasId] = new Chart(ctx.getContext('2d'), {
            type: 'bar',
            data: {
                labels: labels,
                datasets: [{
                    data: values,
                    backgroundColor: colors,
                    borderColor: colors,
                    borderWidth: 2,
                    borderRadius: 4
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                plugins: {
                    legend: {
                        display: false
                    },
                    tooltip: {
                        backgroundColor: 'rgba(0, 0, 0, 0.8)',
                        padding: 12,
                        titleFont: {
                            size: 14,
                            weight: 'bold'
                        },
                        bodyFont: {
                            size: 13
                        },
                        callbacks: {
                            label: function(context) {
                                const value = Math.abs(context.raw);
                                return 'KES ' + value.toLocaleString('en-KE', { minimumFractionDigits: 0, maximumFractionDigits: 0 });
                            }
                        }
                    }
                },
                scales: {
                    x: {
                        grid: {
                            display: false
                        },
                        ticks: {
                            font: {
                                size: 11
                            },
                            maxRotation: 45,
                            minRotation: 45
                        }
                    },
                    y: {
                        beginAtZero: false,
                        grid: {
                            color: 'rgba(0, 0, 0, 0.05)'
                        },
                        ticks: {
                            callback: function(value) {
                                return 'KES ' + value.toLocaleString('en-KE', { minimumFractionDigits: 0, maximumFractionDigits: 0 });
                            },
                            font: {
                                size: 11
                            }
                        }
                    }
                }
            }
        });

        return this.chartInstances[canvasId];
    },

    /**
     * 5. Forecast Chart with Confidence Intervals
     * Shows historical data + forecast + upper/lower bounds
     *
     * @param {string} canvasId - Canvas element ID
     * @param {string[]} historicalLabels - Historical date labels
     * @param {number[]} historicalData - Historical revenue data
     * @param {string[]} forecastLabels - Forecast date labels
     * @param {number[]} forecastData - Forecast values
     * @param {number[]} upperBound - Upper confidence bound
     * @param {number[]} lowerBound - Lower confidence bound
     */
    createForecastChart: function(canvasId, historicalLabels, historicalData, forecastLabels, forecastData, upperBound, lowerBound) {
        this.destroyChart(canvasId);

        const ctx = document.getElementById(canvasId);
        if (!ctx) {
            console.error(`Canvas element with ID '${canvasId}' not found`);
            return null;
        }

        const allLabels = [...historicalLabels, ...forecastLabels];

        this.chartInstances[canvasId] = new Chart(ctx.getContext('2d'), {
            type: 'line',
            data: {
                labels: allLabels,
                datasets: [
                    {
                        label: 'Historical',
                        data: [...historicalData, ...Array(forecastLabels.length).fill(null)],
                        borderColor: '#1976D2',
                        backgroundColor: 'rgba(25, 118, 210, 0.1)',
                        tension: 0.4,
                        borderWidth: 2,
                        fill: false,
                        pointRadius: 3,
                        pointHoverRadius: 5,
                        pointBackgroundColor: '#1976D2',
                        pointBorderColor: '#fff',
                        pointBorderWidth: 2
                    },
                    {
                        label: 'Forecast',
                        data: [...Array(historicalLabels.length).fill(null), ...forecastData],
                        borderColor: '#4CAF50',
                        backgroundColor: 'rgba(76, 175, 80, 0.1)',
                        borderDash: [5, 5],
                        tension: 0.4,
                        borderWidth: 2,
                        fill: false,
                        pointRadius: 3,
                        pointHoverRadius: 5,
                        pointBackgroundColor: '#4CAF50',
                        pointBorderColor: '#fff',
                        pointBorderWidth: 2
                    },
                    {
                        label: 'Upper Bound',
                        data: [...Array(historicalLabels.length).fill(null), ...upperBound],
                        borderColor: 'rgba(76, 175, 80, 0.3)',
                        borderWidth: 1,
                        fill: '+1',
                        backgroundColor: 'rgba(76, 175, 80, 0.1)',
                        tension: 0.4,
                        pointRadius: 0,
                        borderDash: [2, 2]
                    },
                    {
                        label: 'Lower Bound',
                        data: [...Array(historicalLabels.length).fill(null), ...lowerBound],
                        borderColor: 'rgba(76, 175, 80, 0.3)',
                        borderWidth: 1,
                        fill: false,
                        tension: 0.4,
                        pointRadius: 0,
                        borderDash: [2, 2]
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
                            font: {
                                size: 12
                            },
                            filter: function(item, chart) {
                                // Hide upper/lower bound from legend
                                return !item.text.includes('Bound');
                            }
                        }
                    },
                    tooltip: {
                        backgroundColor: 'rgba(0, 0, 0, 0.8)',
                        padding: 12,
                        titleFont: {
                            size: 14,
                            weight: 'bold'
                        },
                        bodyFont: {
                            size: 13
                        },
                        callbacks: {
                            label: function(context) {
                                if (context.dataset.label.includes('Bound')) {
                                    return null;  // Don't show bounds in tooltip
                                }
                                return context.dataset.label + ': KES ' + context.raw.toLocaleString('en-KE', { minimumFractionDigits: 0, maximumFractionDigits: 0 });
                            }
                        },
                        filter: function(tooltipItem) {
                            return !tooltipItem.dataset.label.includes('Bound');
                        }
                    }
                },
                scales: {
                    x: {
                        grid: {
                            display: false
                        },
                        ticks: {
                            font: {
                                size: 11
                            },
                            maxRotation: 45,
                            minRotation: 0
                        }
                    },
                    y: {
                        beginAtZero: true,
                        grid: {
                            color: 'rgba(0, 0, 0, 0.05)'
                        },
                        ticks: {
                            callback: function(value) {
                                return 'KES ' + value.toLocaleString('en-KE', { minimumFractionDigits: 0, maximumFractionDigits: 0 });
                            },
                            font: {
                                size: 11
                            }
                        }
                    }
                }
            }
        });

        return this.chartInstances[canvasId];
    },

    /**
     * Helper: Destroy existing chart instance
     * @param {string} canvasId - Canvas element ID
     */
    destroyChart: function(canvasId) {
        if (this.chartInstances[canvasId]) {
            this.chartInstances[canvasId].destroy();
            delete this.chartInstances[canvasId];
        }
    },

    /**
     * Helper: Destroy all chart instances
     */
    destroyAllCharts: function() {
        Object.keys(this.chartInstances).forEach(canvasId => {
            this.destroyChart(canvasId);
        });
    }
};
