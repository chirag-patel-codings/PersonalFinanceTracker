let _token = getSecurityToken('#reportDataFilterForm');
let reportParametersJSON = {
    reportStartDate: formatDateToYYYYMMDD(new Date(new Date().getFullYear(), 0, 1)),       // correct way!!!,
    reportEndDate: formatDateToYYYYMMDD(new Date(new Date().getFullYear(), 11, 31)),
    reportName: 'IncomeExpense',
    reportInterval: 1,
    tagId: ""
}
let reportDataJSON = null;
let reportsMonthYearJSON = null;
let userCurrencyDetailsJSON = '';
let reportFiltersChanged = false;

document.getElementById('reportStartDate').value = reportParametersJSON.reportStartDate;
document.getElementById('reportEndDate').value = reportParametersJSON.reportEndDate;


// Sets the parameters for the report data retrieval...
const setReportParameters = function () {

    reportParametersJSON.reportStartDate = document.getElementById('reportStartDate').value;
    reportParametersJSON.reportEndDate = document.getElementById('reportEndDate').value;
    // Set the 'queriedReportName' variable
    const radioGroupReportSelection = document.querySelectorAll('input[name="btnradio"]');
    radioGroupReportSelection.forEach((radio) => {
        if (radio.checked) {
            reportParametersJSON.reportName = radio.value;
        }
    });

    // Set the 'queriedReportInterval' variable
    const radioGroupReportIntervalSelection = document.querySelectorAll('input[name="btnradio2"]');

    radioGroupReportIntervalSelection.forEach((radio) => {
        if (radio.checked) {
            reportParametersJSON.reportInterval = +radio.value;
        }
    });

    const tagSelectContainer = document.getElementById('tagSelectContainer');
    // Depends upon visibility.
    if(tagSelectContainer && tagSelectContainer.style.display === 'none') {
        reportParametersJSON.tagId = null;
    }
    else{

        const tagId = document.getElementById('reportTagId');
        reportParametersJSON.tagId = (tagId.value == "" || tagId.value == null) ? null : tagId.value;
    }


}

// Get the tag names and currency details for current user
const getTagsAndCurrencyDetails = function () {

    fetch('Report/GetTagsAndCurrencyDetails', {
        method: 'POST',
        credentials: 'include',
        headers: {
            'Content-Type': 'application/x-www-form-urlencoded',
            'RequestVerificationToken': _token
        },
        body: new URLSearchParams() // No values passed. To Pass key-value pair { connString: value }
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
            populateSelectFromJSON('reportTagId', data.tagsList, 'listOptionId', 'listOptionName', '-- Select Tag --', false);

        })
        .catch(error => {
            console.error("Fetch operation failed:", error);
        });
}

// Returns the sum of items' property
const getSum = function (items, propName) {
    return items.reduce((total, item) => total + (Number(item[propName]) || 0), 0);
}

// Generate 'tr' element
const generateTRElement = function (elementId = null) {

    const trElement = document.createElement('tr');
    if (elementId) {
        trElement.id = elementId;
    }
    return trElement;

}

// Generates the 'td' element for the table
const generateTHTDElement = function (eleType, val, colspan, textBold = false, horizontalAlign = 'left', paddingLeft = '0', backgroundColor, isStickyColumn = false) {

    let tdthEle = document.createElement(eleType);

    tdthEle.innerHTML = textBold ? `<b>${val}</b>` : val;
    tdthEle.colSpan = colspan;
    tdthEle.style.minWidth = '100px';
    tdthEle.style.setProperty('border-left', '1px solid #000');
    tdthEle.style.setProperty('border-right', '1px solid #000');
    tdthEle.style.verticalAlign = 'middle';
    tdthEle.style.textAlign = horizontalAlign;
    tdthEle.style.paddingLeft = paddingLeft;
    if (backgroundColor) {
        tdthEle.style.backgroundColor = backgroundColor;
    }
    if (isStickyColumn) {
        tdthEle.className = 'sticky-col';
    }

    return tdthEle;

}

// Generates and displays Net Summary.
const appendNetSummaryRow = function (reportAmount1, reportAmount2, netSummaryRowHeaderText) {

    const tbody = document.getElementById("reportsTBody");
    let newTD = null;
    let currentRow = null;

    const headingBackgroundColor = '#f5f5f5';

    currentRow = document.getElementById('netSummary') ? document.getElementById('netSummary') : generateTRElement('netSummary');
    currentRow.style.setProperty('border-bottom', '1px solid #000', 'important');

    if (currentRow.childElementCount == 0) {
        newTD = generateTHTDElement('td', netSummaryRowHeaderText, 1, false, 'center', 5, headingBackgroundColor, true);
        newTD.style.setProperty('border-bottom', '1px solid #000', 'important');
        currentRow.appendChild(newTD);
    }

    newTD = generateTHTDElement('td', `${userCurrencyDetailsJSON.currencySymbol}${Number(reportAmount2).toFixed(2)}`, 1, true, 'right', 0, headingBackgroundColor, false);
    currentRow.appendChild(newTD);

    if (reportParametersJSON.reportName != 'NetWorth') {
        newTD = generateTHTDElement('td', `${userCurrencyDetailsJSON.currencySymbol}${Number(reportAmount1).toFixed(2)}`, 1, true, 'right', 0, headingBackgroundColor, false);
        currentRow.appendChild(newTD);
    }
    tbody.appendChild(currentRow);

}

// Append the data rows with their summary...
const appendDataRowsWithSummary = function (filteredData, summaryRowId, summaryRowHeaderText) {
    // Get the table header & body element
    const tbody = document.getElementById("reportsTBody");
    let newTD = null;
    let currentRow = null;

    let dataRowId = '';
    const headingBackgroundColor = '#f5f5f5';

    // Categories
    for (let i = 0; i < filteredData.length; i++) {

        dataRowId = 'data-' + filteredData[i].reportRowNo;
        currentRow = document.getElementById(dataRowId) ? document.getElementById(dataRowId) : generateTRElement(dataRowId);

        if (currentRow.childElementCount == 0) {
            newTD = generateTHTDElement('td', filteredData[i].reportName, 1, false, 'left', 15, headingBackgroundColor, true);
            newTD.style.setProperty('font-weight', 'normal', 'important');
            currentRow.appendChild(newTD);
        }

        // amount2 is the 'Actual'
        newTD = generateTHTDElement('td', `${userCurrencyDetailsJSON.currencySymbol}${Number(filteredData[i].reportAmount2).toFixed(2)}`, 1, false, 'right', 0, null, false);
        currentRow.appendChild(newTD);

        if (reportParametersJSON.reportName != 'NetWorth') {
            // Amount 1 is budget/goal...Not Displayed for NetWorth report
            newTD = generateTHTDElement('td', `${userCurrencyDetailsJSON.currencySymbol}${Number(filteredData[i].reportAmount1).toFixed(2)}`, 1, false, 'right', 0, null, false);
            currentRow.appendChild(newTD);
        }

        tbody.appendChild(currentRow);

    }

    // Summary Row
    if (filteredData.length > 0) {

        currentRow = document.getElementById('summaryRow-' + summaryRowId) ? document.getElementById('summaryRow-' + summaryRowId) : generateTRElement('summaryRow-' + summaryRowId);
        currentRow.classList.add('top-bottom-single-border');

        if (currentRow.childElementCount == 0) {
            newTD = generateTHTDElement('td', summaryRowHeaderText, 1, true, 'center', 0, headingBackgroundColor, true);
            currentRow.appendChild(newTD);
        }
        // amount2 is the 'Actual'
        newTD = generateTHTDElement('td', `${userCurrencyDetailsJSON.currencySymbol}${getSum(filteredData, 'reportAmount2').toFixed(2)}`, 1, true, 'right', 0, headingBackgroundColor, false);
        currentRow.appendChild(newTD);

        // Amount 1 is budget/goal...Not Displayed for NetWorth report
        if (reportParametersJSON.reportName != 'NetWorth') {
            newTD = generateTHTDElement('td', `${userCurrencyDetailsJSON.currencySymbol}${getSum(filteredData, 'reportAmount1').toFixed(2)}`, 1, true, 'right', 0, headingBackgroundColor, false);
            currentRow.appendChild(newTD);
        }

        tbody.appendChild(currentRow);
    }

}

// Displays the actual report data
const displayReportData = function (data) {

    // Get the table header & body element
    const thead = document.getElementById("reportsTHead");
    const tbody = document.getElementById("reportsTBody");

    thead.innerHTML = "";
    tbody.innerHTML = "";

    if (data.length > 0) {

        // For 'Month Year'
        let headerRow0 = generateTRElement('headerRow0');
        headerRow0.style.setProperty('border-top', '1px solid #000', 'important');
        headerRow0.style.setProperty('border-bottom', '1px solid #000', 'important');

        let headerRow0Col0Content = '';
        if (reportParametersJSON.reportName == 'IncomeExpense') {
            headerRow0Col0Content = 'Categories';
        }
        else if (reportParametersJSON.reportName == 'NetWorth') {
            headerRow0Col0Content = 'Assets & Liabilities';
        }
        else {
            headerRow0Col0Content = 'Goal';
        }

        // For 'Actual', 'Budget'
        let headerRow1 = generateTRElement('headerRow1');
        headerRow1.style.setProperty('border-top', '1px solid #000', 'important');
        headerRow1.style.setProperty('border-bottom', '1px solid #000', 'important');

        const headingBackgroundColor = '#f5f5f5';

        let colCounter = Math.max(...data.map(x => x.reportRowNo));     // Records per month

        // HEADER ROW 0: Add 'Month Year' Row
        if (data[0].reportMonthYear != null || reportParametersJSON.reportName == 'NetWorth') {

            // Monthly Report (If data has 'reportMonthYear') 
            if (headerRow0.childElementCount == 0) {
                let header0RowCol0 = generateTHTDElement('th', headerRow0Col0Content, 1, true, 'left', 5, headingBackgroundColor, true);
                if (reportParametersJSON.reportName == 'NetWorth'){

                    header0RowCol0.style.setProperty('border-bottom', '1px solid #000', 'important');
                }
                headerRow0.appendChild(header0RowCol0);
            };

            for (let i = 0; i < data.length; i = i + colCounter) {
                let headerRow0NewTH = generateTHTDElement('th', reportParametersJSON.reportInterval == 1 ? data[i].reportMonthYear : 'Whole Period', reportParametersJSON.reportName != 'NetWorth' ? 2 : 1, true, 'center', 0, headingBackgroundColor, false);
                headerRow0.appendChild(headerRow0NewTH);
            }

            thead.appendChild(headerRow0);

        }

        // HEADER ROW 1 (Monthly or Whole Period)
        // Append First Cell
        if (reportParametersJSON.reportName != 'NetWorth') {

            let headerRow1NewTH2Text = reportParametersJSON.reportName == 'IncomeExpense' ? 'Budget' : 'Goal';
            if (headerRow1.childElementCount == 0) {
                let cellContents = reportParametersJSON.reportInterval == 1 ? '' : headerRow0Col0Content;
                let header1RowCol0 = generateTHTDElement('th', cellContents, 1, true, 'left', 5, headingBackgroundColor, true);
                header1RowCol0.style.setProperty('border-bottom', '1px solid #000', 'important');
                headerRow1.appendChild(header1RowCol0);
            }

            let amountColsRepeatCounter = data[0].reportMonthYear != null ? headerRow0.childElementCount - 1 : 1;

            for (let i = 0; i < amountColsRepeatCounter; i++) {
                let headerRow1NewTH1 = generateTHTDElement('th', 'Actual', 1, true, 'center', 0, headingBackgroundColor, false);
                let headerRow1NewTH2 = generateTHTDElement('th', headerRow1NewTH2Text, 1, true, 'center', 0, headingBackgroundColor, false);

                headerRow1.appendChild(headerRow1NewTH1);
                headerRow1.appendChild(headerRow1NewTH2);
            }

            thead.appendChild(headerRow1);

        }

        // Pending: Place in earlier code
        let dataRowSummary0HeaderText = '';
        let dataRowSummary1HeaderText = '';
        let netSummaryRowHeaderText = '';
        if (reportParametersJSON.reportName == 'IncomeExpense') {
            dataRowSummary0HeaderText = 'Total Income';
            dataRowSummary1HeaderText = 'Total Expense';
            netSummaryRowHeaderText = 'Net Income';
        }
        else if (reportParametersJSON.reportName == 'NetWorth') {
            dataRowSummary0HeaderText = 'Total Assets';
            dataRowSummary1HeaderText = 'Total Liabilities';
            netSummaryRowHeaderText = 'Net Worth';
        }

        // DETAILS AND SUMMARY ROWS:
        if (data[0].reportMonthYear != null) {  // Report is monthly (If data has 'reportMonthYear')

            for (let i = 0; i < data.length; i = i + colCounter) {
                let monthYearData = data.filter(d => d.reportMonthYear == data[i].reportMonthYear);
                if (reportParametersJSON.reportName != 'Goal') {
                    let incomeCreditData = monthYearData.filter(d => d.reportClassification == '1');
                    let expenseDebitData = monthYearData.filter(d => d.reportClassification == '-1');
                    appendDataRowsWithSummary(incomeCreditData, 0, dataRowSummary0HeaderText);
                    appendDataRowsWithSummary(expenseDebitData, 1, dataRowSummary1HeaderText);
                    appendNetSummaryRow(getSum(incomeCreditData, 'reportAmount1') + getSum(expenseDebitData, 'reportAmount1'), getSum(incomeCreditData, 'reportAmount2') + getSum(expenseDebitData, 'reportAmount2'), netSummaryRowHeaderText);
                }
                else {
                    appendDataRowsWithSummary(monthYearData, 0, dataRowSummary0HeaderText);
                }
            }

        }
        else {   // Total Period

            for (let i = 0; i < data.length; i = i + colCounter) {
                if (reportParametersJSON.reportName != 'Goal') {
                    let incomeCreditData = data.filter(d => d.reportClassification == '1');
                    let expenseDebitData = data.filter(d => d.reportClassification == '-1');
                    appendDataRowsWithSummary(incomeCreditData, 0, dataRowSummary0HeaderText);
                    appendDataRowsWithSummary(expenseDebitData, 1, dataRowSummary1HeaderText);
                    appendNetSummaryRow(getSum(incomeCreditData, 'reportAmount1') + getSum(expenseDebitData, 'reportAmount1'), getSum(incomeCreditData, 'reportAmount2') + getSum(expenseDebitData, 'reportAmount2'), netSummaryRowHeaderText);
                }
                else {
                    appendDataRowsWithSummary(data, 0, dataRowSummary0HeaderText);
                }

            }
        }
    }

}
    // Gets all the report data from start date to end date...
    const getReport = function () {

        // Set the values of 'reportParametersJSON' object
        setReportParameters();  

        // add a return before fetch and pass the data through the final link of the chain and extract using 'then';
        fetch('Report/GetReport', {
            method: 'POST',
            headers: {
                "credentials": "include",
                "Content-Type": "application/json",
                "RequestVerificationToken": _token
            },
            body: JSON.stringify(reportParametersJSON)
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
                reportDataJSON = data.report;
                reportsMonthYearJSON = data.reportMonthYear;
                // Work with parsed data object here
                // console.log('data:', data)
                displayReportData(reportDataJSON);

            })
            .catch(error => {
                console.error("Fetch operation failed:", error);
            });

    }


    getTagsAndCurrencyDetails();
    getReport();


    // show/hide the tag selection based on the report type selected
    document.querySelectorAll('input[name="btnradio"]').forEach(radio => {
        radio.addEventListener("change", function () {
            let tagElement = document.getElementById('tagSelectContainer');
            if (this.id == 'btnRadioIncomeExpense' && this.checked) {
                tagElement.style.display = 'block';
            }
            else {
                tagElement.style.display = 'none';
            }
        });
    });

    // Get the report on click of the 'getReport' button
    document.getElementById('viewReport').addEventListener('click', (event) => {

        event.preventDefault();

        if (isValidForm('reportDataFilterForm')) {
            getReport();    // Parameter values are set by other elements
        }

    });

    // Requery report data
    document.getElementById("reportDataFilterDiv").addEventListener("keypress", (e) => {

        if ((e.target.tagName === "INPUT" || e.target.tagName === "SELECT") && e.key === "Enter") {

            e.preventDefault();
            
            if (isValidForm('reportDataFilterForm')) {
                getReport();    // Parameter values are set by other elements
            }
            
        }
    });

    appendLessThanDateValueValidationFunctionality();

    $(document).ready(function () {

        // Check validation on Save click
        $('#btnTransactionModalSaveChanges').on('click', function () {
            const $form = $('#addEditTransactionForm');
            if (!$form.valid()) {
                return;
            }
            console.log("Form is valid! Sending AJAX payload...");
            saveTransactionRecord();

        });

    });

