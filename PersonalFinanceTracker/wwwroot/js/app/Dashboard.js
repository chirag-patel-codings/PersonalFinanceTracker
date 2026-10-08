let _token = getSecurityToken('#dashboardDataFilterForm');
let dashboardParametersJSON = {
    dashboardParametersStartDate: formatDateToYYYYMMDD(new Date(new Date().getFullYear(), 0, 1)),       // correct way!!!
    dashboardParametersEndDate: formatDateToYYYYMMDD(new Date(new Date().getFullYear(), 11, 31)),
}
let monthlyIncomeExpenseDashboardDataJSON = null;
let userCurrencyDetailsJSON = '';
let dashboardFiltersChanged = false;

document.getElementById('dashboardStartDate').value = dashboardParametersJSON.dashboardParametersStartDate;
document.getElementById('dashboardEndDate').value = dashboardParametersJSON.dashboardParametersEndDate;


// Retrieve Currency Details and License Key for the current user
const getCurrencyDetailsAndLicenseKey = function () {

    fetch('/Dashboard/GetUserCurrencyDetailsAndLicenseKey', {
        method: 'POST',
        credentials: "include",   // REQUIRED for antiforgery cookie
        headers: {
            "RequestVerificationToken": _token
        }

    })
        .then(response => {
            // Check if the HTTP status code is 200-299 (Ok)
            if (!response.ok) {
                throw new Error(`HTTP error! Status: ${response.status}`);
            }
            // Parse the body text as JSON
            return response.json();
        })
        .then(data => {

            userCurrencyDetailsJSON = data.userCurrencyDetails;
            ej.base.registerLicense(data.licenseKey);

        })
        .catch(error => {
            console.error("Fetch operation failed:", error);
        });
}

// PIE CHART: Function to render the Category-wise Expense chart
const renderCategoryWiseExpenseChart = function (rawData) {

    const existingChart = document.getElementById('categoryWiseReportChartDiv');
    if (existingChart && existingChart.ej2_instances && existingChart.ej2_instances[0]) {
        existingChart.ej2_instances[0].destroy();
    }

    // Inject AccumulationLegend & PieSeries modules if loading as ES modules/CDN
    ej.charts.AccumulationChart.Inject(ej.charts.AccumulationLegend, ej.charts.PieSeries, ej.charts.AccumulationTooltip, ej.charts.AccumulationDataLabel);

    const pieChart = new ej.charts.AccumulationChart({
        theme: 'Bootstrap5',
        series: [{
            dataSource: rawData,
            xName: 'reportName',
            yName: 'reportAmount2',
            // INCREASE PIE CHART DIAMETER (Default is ~70%)
            radius: '85%',
            pointColorMapping: 'reportColor',
            dataLabel: {
                visible: true,
                position: 'Inside'
            }
        }],
        // MOVE LEGEND TO THE BOTTOM
        legendSettings: {
            visible: true,
            position: 'Bottom',  // Options: 'Top', 'Bottom', 'Left', 'Right', 'Auto'
            alignment: 'Center'  // Centers legend items horizontally at the bottom
        },

        textRender: function (args) {
            args.text = `${userCurrencyDetailsJSON.currencySymbol}${args.point.y} (${args.point.percentage}%)`;
        },

        tooltip: {
            enable: true,
            // format: '${point.x}: <b>' + userCurrencyDetailsJSON.currencySymbol + '{point.y}</b>' 
            format: `\${point.x}: <b>${userCurrencyDetailsJSON.currencySymbol}\${point.y} (\${point.percentage}%)</b>`
        },
        title: 'Category-wise Breakdown'
    });

    pieChart.appendTo('#categoryWiseReportChartDiv');

}

// Bar (Grouped Column) Chart: Function to render the Income vs Expense chart
const renderIncomeExpenseChart = function(rawData) {

    // Filter helper for Category ("Income" vs "Expense")
    const filterByCategory = (category) =>
        rawData.filter(item => item.category === category);

    const chart = new ej.charts.Chart({
        theme: 'Bootstrap5',
        primaryXAxis: {
            valueType: 'Category',
            title: 'Month / Year',
            labelRotation: 90,
            // labelIntersectAction: 'Rotate90',       // Options: 'Rotate45', 'Rotate90', 'Wrap', 'MultipleRows', 'None'
            labelStyle: {
                size: '9px'
            }
        },
        primaryYAxis: {
            title: `Amount (${userCurrencyDetailsJSON.currencySymbol})`,
            labelFormat: `${userCurrencyDetailsJSON.currencySymbol}{value}`
        },

        // Side-by-side grouped columns for Income and Expense
        series: [
            {
                type: 'Column',
                dataSource: filterByCategory('Income'),
                xName: 'monthYear',
                yName: 'amount',
                name: 'Income',
                fill: '#28a745', // Green column for Income

            },
            {
                type: 'Column',
                dataSource: filterByCategory('Expense'),
                xName: 'monthYear',
                yName: 'amount',
                name: 'Expense',
                fill: '#dc3545', // Red column for Expense
            }
        ],
        tooltip: {
            enable: true,
            shared: true // Shows Income and Expense side-by-side in tooltip
        },
        legendSettings: {
            visible: true,
            position: 'Top'
        },
        title: 'Income vs. Expense by Month'
    });

    chart.appendTo('#monthlyIncomeExpenseReportChartDiv');
}

// Line Chart: Function to render the Net Income chart
const renderNetIncomeChart = function (rawData) {

    const lineChart = new ej.charts.Chart({
        theme: 'Bootstrap5', // Applies Bootstrap 5 styling

        primaryXAxis: {
            
            valueType: 'Category',
            title: 'Month / Year',
            
            // FORCES 90-degree label rotation regardless of overlap
            labelRotation: 90,
            // labelIntersectAction: 'Rotate90',       // Options: 'Rotate45', 'Rotate90', 'Wrap', 'MultipleRows', 'None'
            labelStyle: {
                size: '9px'
            }
        },
        primaryYAxis: {
            title: `Amount (${userCurrencyDetailsJSON.currencySymbol})`,
            labelFormat: `${userCurrencyDetailsJSON.currencySymbol}{value}`
        },

        series: [
            {
                type: 'Line', // <--- Use 'Line' to connect data points with lines
                dataSource: rawData,
                xName: 'monthYear',
                yName: 'amount',
                name: 'Net-Income Trend',
                width: 3,             // Line thickness in pixels
                fill: '#0d6efd',       // Bootstrap 5 Primary Blue line color

                // Configures visible dots at each data point
                marker: {
                    visible: true,
                    width: 5,
                    height: 5,
                    shape: 'Circle',
                    
                }
            }
        ],

        tooltip: { enable: true },
        legendSettings: { visible: true, position: 'Top' },
        title: 'Net Income Trend'
    });

    lineChart.appendTo('#netIncomeReportChartDiv');
}


// Line Chart: Function to render the Net Worth chart
const renderNetWorthChart = function (rawData) {

    const lineChart = new ej.charts.Chart({
        theme: 'Bootstrap5', // Applies Bootstrap 5 styling

        primaryXAxis: {
            
            valueType: 'Category',
            title: 'Month / Year',
            
            // FORCES 90-degree label rotation regardless of overlap
            labelRotation: 90,
            // labelIntersectAction: 'Rotate90',       // Options: 'Rotate45', 'Rotate90', 'Wrap', 'MultipleRows', 'None'
            labelStyle: {
                size: '9px'
            }
        },
        primaryYAxis: {
            title: `Amount (${userCurrencyDetailsJSON.currencySymbol})`,
            labelFormat: `${userCurrencyDetailsJSON.currencySymbol}{value}`
        },

        series: [
            {
                type: 'Line', // <--- Use 'Line' to connect data points with lines
                dataSource: rawData,
                xName: 'monthYear',
                yName: 'amount',
                name: 'Net-Worth Trend',
                width: 3,             // Line thickness in pixels
                fill: '#7323f3',       // Purple line color

                // Configures visible dots at each data point
                marker: {
                    visible: true,
                    width: 5,
                    height: 5,
                    shape: 'Circle',
                    
                }
            }
        ],

        tooltip: { enable: true },
        legendSettings: { visible: true, position: 'Top' },
        title: 'Net Worth Trend'
    });

    lineChart.appendTo('#netWorthReportChartDiv');
}

// Retrieve Currency Details and License Key for the current user
const getReportsData = function () {

    dashboardParametersJSON.dashboardParametersStartDate = document.getElementById('dashboardStartDate').value;
    dashboardParametersJSON.dashboardParametersEndDate = document.getElementById('dashboardEndDate').value;

    fetch('/Dashboard/GetReportsData', {
        method: 'POST',
        credentials: "include",   // REQUIRED for antiforgery cookie
        headers: {
            "Content-Type": "application/json",
            "RequestVerificationToken": _token
        },
        body: JSON.stringify(dashboardParametersJSON)

    })
        .then(response => {
            // Check if the HTTP status code is 200-299 (Ok)
            if (!response.ok) {
                throw new Error(`HTTP error! Status: ${response.status}`);
            }
            // Parse the body text as JSON
            return response.json();
        })
        .then(data => {

            renderCategoryWiseExpenseChart(data.categoryWiseSummaryChartData);
            renderIncomeExpenseChart(data.monthlyIncomeExpenseChartData);
            renderNetIncomeChart(data.monthlyNetIncomeChartData);
            renderNetWorthChart(data.monthlyNetWorthChartData);
        })
        .catch(error => {
            console.error("Fetch operation failed:", error);
        });
}

// Initialize upon page load
getCurrencyDetailsAndLicenseKey();
getReportsData();

// Requery report data
document.getElementById("dashboardDataFilterDiv").addEventListener("keypress", (e) => {

    if (e.target.tagName === "INPUT" && e.key === "Enter") {

        e.preventDefault();

        if (isValidForm('dashboardDataFilterForm')) {
            getReportsData();    // Parameter values are set by other function
        }

    }
});

appendLessThanDateValueValidationFunctionality();