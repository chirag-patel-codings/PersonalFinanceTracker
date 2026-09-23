let _token = getSecurityToken('#budgetDataFilterForm');
let budgetDetailsDataJSON = '';
let budgetsMonthYearJSON = '';
let userCurrencyDetailsJSON = '';
let budgetStartDate = formatDateToYYYYMMDD(new Date(new Date().getFullYear(), 0, 1));       // correct way!!!
let budgetEndDate = formatDateToYYYYMMDD(new Date(new Date().getFullYear(), 11, 31));       // correct way!!!
let budgetFiltersChanged = false;

// Generates/Creates a new modal and shows or hide the same modal...
const showHideAddEditBudgetSettingsModal = function (show) {

    const addEditBudgetSettingsModalElement = document.getElementById('addEditBudgetSettingsModal');

    // Get the existing instance, or create a new one if it doesn't exist yet
    let modalInstance = bootstrap.Modal.getInstance(addEditBudgetSettingsModalElement);

    if (!modalInstance) {
        modalInstance = new bootstrap.Modal(addEditBudgetSettingsModalElement);
    }

    if (show) {

        modalInstance.show();

    } else {

        modalInstance.hide();
    }

}

// Display the modal with Data
const showAddEditBudgetSettingsModal = function (data) {

    const formsContainerDiv = document.getElementById('addEditBudgetSettingsModalBodyContainerId');
    formsContainerDiv.className = 'table-container';
    formsContainerDiv.style = 'background-color: rgb(244, 246, 249);';
    formsContainerDiv.innerHTML = "";

    let intCnt = 0;
    let drawBorderTop = false;

    data.forEach(record => {
        drawBorderTop = intCnt == 0 ? false : true;
        formsContainerDiv.appendChild(generateBudgetSettingsForms(record, drawBorderTop));
        intCnt++;
    });

    // Open up the Bootstrap modal visually
    showHideAddEditBudgetSettingsModal(true);
}

const enableDisableUnderOverSelectSettings = function (isChecked, formId) {

    const budgetIfUnderSettingsSelect = document.querySelector(`#${formId} #modalBudgetIfUnderSettingsId`);
    const budgetIfOverSettingsSelect = document.querySelector(`#${formId} #modalBudgetIfOverSettingsId`);
    const saveBtn = document.querySelector(`#${formId} #btnBudgetSettingsModalSaveChanges`);

    if (isChecked == false) {

        budgetIfUnderSettingsSelect.value = 0;
        budgetIfOverSettingsSelect.value = 0;

    }

    budgetIfUnderSettingsSelect.disabled = !isChecked;
    budgetIfOverSettingsSelect.disabled = !isChecked;

    saveBtn.disabled = false;
};

// Generates the forms for all the budget settings records. Single form for each record!!!
const generateBudgetSettingsForms = function (record, drawBorderTop) {

    // Create the main form element
    const form = document.createElement('form');
    form.id = 'form-' + record.categoryId;
    form.setAttribute('data-val', 'true');
    form.style = drawBorderTop ? "border-top: 1px solid #e0e0e0;" : '';

    // Create the hidden Category ID field
    const hiddenId = document.createElement('input');
    hiddenId.type = 'hidden';
    hiddenId.id = 'modalCategoryId';
    hiddenId.name = 'categoryId';
    hiddenId.value = record.categoryId; // Populate dynamically
    form.appendChild(hiddenId);

    // Create the main structural row container
    const rowDiv = document.createElement('div');
    rowDiv.className = 'row p-1';

    // --- COLUMN 1: Category Name Label ---
    const col1 = document.createElement('div');
    col1.className = 'col-sm field pe-3 pb-3 d-flex align-items-center';
    const categoryNameLabel = document.createElement('label');
    categoryNameLabel.id = 'modalCategoryNameId';
    categoryNameLabel.name = 'categoryName';
    categoryNameLabel.className = 'input-label';
    categoryNameLabel.textContent = record.categoryName; // Populate dynamically
    col1.appendChild(categoryNameLabel);
    rowDiv.appendChild(col1);

    // --- COLUMN 2: Range Slider Control ---
    // Column wrapper
    const col2 = document.createElement('div');
    col2.className = 'col-sm field pt-3';

    // Bootstrap form-switch wrapper
    const switchDiv = document.createElement('div');
    switchDiv.className = 'form-check form-switch';

    // Checkbox input (Bootstrap toggle)
    const budgetToBeRolledOverSwitchInput = document.createElement('input');
    budgetToBeRolledOverSwitchInput.type = 'checkbox';
    budgetToBeRolledOverSwitchInput.className = 'form-check-input toggle-switch';
    budgetToBeRolledOverSwitchInput.id = 'budgetToBeRolledOverSwitch';
    budgetToBeRolledOverSwitchInput.name = 'budgetToBeRolledOver';
    budgetToBeRolledOverSwitchInput.checked = record.budgetToBeRolledOver == "1";
    budgetToBeRolledOverSwitchInput.value = budgetToBeRolledOverSwitchInput.checked ? 1 : 0;

    budgetToBeRolledOverSwitchInput.addEventListener('change', (e) => {
        e.preventDefault();
        const isChecked = e.target.checked;
        e.target.value = isChecked ? 1 : 0;
        enableDisableUnderOverSelectSettings(isChecked, 'form-' + record.categoryId);
    });

    // Label
    const budgetToBeRolledOverSwitchLabel = document.createElement('label');
    budgetToBeRolledOverSwitchLabel.className = 'form-check-label input-label mt-3';
    budgetToBeRolledOverSwitchLabel.htmlFor = 'budgetToBeRolledOverSwitch';
    budgetToBeRolledOverSwitchLabel.textContent = '* Rollover';

    // Build structure
    switchDiv.appendChild(budgetToBeRolledOverSwitchInput);
    switchDiv.appendChild(budgetToBeRolledOverSwitchLabel);
    col2.appendChild(switchDiv);
    rowDiv.appendChild(col2);


    // --- COLUMN 3: Budget If Under Dropdown ---
    const col3 = document.createElement('div');
    col3.className = 'col-sm field pt-2';

    const selectBudgetIfUnderSettings = document.createElement('select');
    selectBudgetIfUnderSettings.id = 'modalBudgetIfUnderSettingsId';
    selectBudgetIfUnderSettings.name = 'budgetIfUnderSettings';
    selectBudgetIfUnderSettings.className = 'form-control';
    selectBudgetIfUnderSettings.setAttribute('placeholder', ' ');

    // Add dropdown options and set active selection conditionally
    const underOpt1 = new Option('Add to next month', '1');
    const underOpt0 = new Option("Don't Carry Over", '0');
    selectBudgetIfUnderSettings.add(underOpt1);
    selectBudgetIfUnderSettings.add(underOpt0);
    selectBudgetIfUnderSettings.value = record.budgetIfUnderSettings;
    selectBudgetIfUnderSettings.style.width = '200px';

    const labelBudgetIfUnderSettings = document.createElement('label');
    labelBudgetIfUnderSettings.htmlFor = 'modalBudgetIfUnderSettingsId';
    labelBudgetIfUnderSettings.className = 'input-label mt-2';
    labelBudgetIfUnderSettings.textContent = '* If Under Expenses';

    selectBudgetIfUnderSettings.disabled = !budgetToBeRolledOverSwitchInput.checked;

    selectBudgetIfUnderSettings.addEventListener('change', (e) => {
        e.preventDefault();
        if (e.target.disabled == false) {
            document.querySelector(`#${form.id} #btnBudgetSettingsModalSaveChanges`).disabled = false;
        }
    });

    col3.appendChild(selectBudgetIfUnderSettings);
    col3.appendChild(labelBudgetIfUnderSettings);
    rowDiv.appendChild(col3);

    // --- COLUMN 4: Budget If Over Dropdown ---
    const col4 = document.createElement('div');
    col4.className = 'col-sm field pt-2';

    const selectBudgetIfOverSettings = document.createElement('select');
    selectBudgetIfOverSettings.id = 'modalBudgetIfOverSettingsId';
    selectBudgetIfOverSettings.name = 'budgetIfOverSettings';
    selectBudgetIfOverSettings.className = 'form-control';
    selectBudgetIfOverSettings.setAttribute('placeholder', ' ');

    const overOpt1 = new Option('Subtract from next month', '1');
    const overOpt0 = new Option("Don't Carry Over", '0');
    selectBudgetIfOverSettings.add(overOpt1);
    selectBudgetIfOverSettings.add(overOpt0);
    selectBudgetIfOverSettings.value = record.budgetIfOverSettings;
    selectBudgetIfOverSettings.style.width = '225px';

    const labelOver = document.createElement('label');
    labelOver.htmlFor = 'modalBudgetIfOverSettingsId';
    labelOver.className = 'input-label mt-2';
    labelOver.textContent = '* If Over Expenses';

    selectBudgetIfOverSettings.disabled = !budgetToBeRolledOverSwitchInput.checked;

    selectBudgetIfOverSettings.addEventListener('change', (e) => {
        e.preventDefault();
        if (e.target.disabled == false) {
            document.querySelector(`#${form.id} #btnBudgetSettingsModalSaveChanges`).disabled = false;
        }
    });

    col4.appendChild(selectBudgetIfOverSettings);
    col4.appendChild(labelOver);
    rowDiv.appendChild(col4);

    const col5 = document.createElement('div');
    col5.className = 'col-sm field pt-1';
    // --- ACTION BUTTON: Save Trigger ---
    const saveBtn = document.createElement('button');
    saveBtn.type = 'button';
    saveBtn.className = 'btn btn-primary';
    saveBtn.style.height = '50px';
    saveBtn.id = 'btnBudgetSettingsModalSaveChanges';
    // saveBtn.textContent = '💾 Save';
    saveBtn.innerHTML = iconText('save', 'Save');
    saveBtn.disabled = true;
    saveBtn.addEventListener('click', (e) => {
        e.preventDefault();
        const record = getFormEntries(form.id);
        saveBudgetSettings(record);
        // console.log('record: ', record);
        // e.target.disabled = true;
        e.currentTarget.disabled = true;
    });

    col5.appendChild(saveBtn);
    rowDiv.appendChild(col5);

    // Final Assembly
    form.appendChild(rowDiv);
    return form;

}


// To retrieve all the budget settings
const getBudgetSettings = function () {

    // add a return before fetch and pass the data through the final link of the chain and extract using 'then';
    fetch('/Budget/GetBudgetSettings', {
        method: 'POST',
        headers: {
            "credentials": "include",
            "Content-Type": "application/json",
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
            showAddEditBudgetSettingsModal(data);
        })
        .catch(error => {
            console.error("Fetch operation failed:", error);
        });

}

// Saves the current budget setting to the database!
const saveBudgetSettings = function (record) {

    // record.budgetToBeRolledOver = isNaN(record.budgetToBeRolledOver) ? 0 : Number(record.budgetToBeRolledOver);     // Change data type to number
    record.budgetToBeRolledOver = Number(record.budgetToBeRolledOver);     // Change data type to number
    record.budgetIfUnderSettings = Number(record.budgetIfUnderSettings);
    record.budgetIfOverSettings = Number(record.budgetIfOverSettings);
    //console.log('record - saveBudgetSettings: ', record);

    fetch("/Budget/SaveBudgetSettings", {
        method: "POST",
        credentials: "include",   // REQUIRED for antiforgery cookie
        body: JSON.stringify(record),
        headers: {
            "Content-Type": "application/json",
            "RequestVerificationToken": _token
        }
    })
        .then(res => res.json())
        .then(result => {
            if (result.success) {
                showDbMessage("Budget setting updated!!!", true);
            }
            else {
                console.error(result.message);
                showDbMessage("Error: Budget setting has not been updated!!!", false);
                return;
            }

        });
}

// Gets all the budget data from start date to end date...
const getBudgetDetails = function (budgetStartDate, budgetEndDate) {

    // add a return before fetch and pass the data through the final link of the chain and extract using 'then';
    fetch('/budgets', {
        method: 'POST',
        headers: {
            "credentials": "include",
            "Content-Type": "application/json",
            "RequestVerificationToken": _token
        },
        body: JSON.stringify({ budgetDetailsStartDate: budgetStartDate, budgetDetailsEndDate: budgetEndDate })
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
            budgetsMonthYearJSON = data.budgetsMonthYear;
            budgetDetailsDataJSON = data.budgetDetails;
            userCurrencyDetailsJSON = data.userCurrencyDetails;
            // Work with parsed data object here
            displayBudgetData(budgetDetailsDataJSON, budgetsMonthYearJSON);

        })
        .catch(error => {
            console.error("Fetch operation failed:", error);
        });

}

// Displays the budget data table header with month year
const displayBudgetDataHeader = function (budgetsMonthYear) {

    const theadRow = document.getElementById("budgetsTHeadRow");
    theadRow.innerHTML = "";

    // ADD HEADER
    let theadNewCols = '<th class ="sticky-col">Categories</th>';

    budgetsMonthYear.forEach((item) => {
        theadNewCols += `<th>${item}</th>`;
    });

    // ADD DETAILS/ROW
    theadRow.innerHTML = theadNewCols;
}

// Displays the actual budget data
const displayBudgetDataDetails = function (budgetDetailsData) {

    // Select the table body element
    const tbody = document.getElementById("budgetsTBody");

    let prevCategoryId = '';
    let row = null;
    let tCols = '';

    // Iterate over the new data and append new rows
    budgetDetailsData.forEach(item => {

        let currentCategoryId = item.categoryId;
        if (currentCategoryId !== prevCategoryId) {

            if (row) {
                row.innerHTML = tCols;
                tbody.appendChild(row);
                row = null;
                tCols = '';
            }

            row = document.createElement("tr");
            tCols = `<td class = "sticky-col">${item.categoryName}</td>`;
            tCols += `<td><input type="number" step="1" id=${item.budgetId}-${item.budgetDateDigits} value=${item.budgetAmount ?? ''}></td>`;

            prevCategoryId = currentCategoryId;

        }
        else {

            tCols += `<td><input type="number" step="1" id=${item.budgetId}-${item.budgetDateDigits} value=${item.budgetAmount ?? ''}>`;

        }

    });

    // Append the last row...
    row.innerHTML = tCols;
    tbody.appendChild(row);
    row = null;
    tCols = '';

}

// First time load only!!!
const generateBudgetDataSummaryRows = function (budgetSummaryData, categoryType, cellText, idPrefix) {

    // Select the table body element
    const tbody = document.getElementById("budgetsTBody");
    row = document.createElement("tr");

    // ADD HEADER
    let trNewCols = `<td class ="sticky-col">${cellText}</td>`;

    // Append Details
    budgetSummaryData.forEach((item) => {
        trNewCols += `<td class="data-summary" id=${idPrefix + item.budgetDateDigits}>${userCurrencyDetailsJSON.currencySymbol}${categoryType != "" ? getSumOfICategoryTypePerMonth(categoryType, item.budgetDateDigits) : getNetSummary(item.budgetDateDigits)}</td>`;
    });

    // ADD DETAILS/ROW
    row.innerHTML = trNewCols;
    tbody.appendChild(row);

}

// Displays the data in the table in UI (<tbody id="budgetsTBody">)...
const displayBudgetData = function (budgetDetailsData, budgetsMonthYear) {

    // Clear the existing contents!!!
    document.getElementById("budgetsTBody").innerHTML = "";
    const incomeCategoryType = "1";
    const expenseCategoryType = "-1";

    // Get the budget key
    const budgetDateDigits = [...new Set(budgetDetailsData.map(x => x.budgetDateDigits))]
        .map(budgetDateDigits => ({ budgetDateDigits }));

    // append headers
    displayBudgetDataHeader(budgetsMonthYear);

    // income
    const incomeBudgetData = budgetDetailsData.filter(i => i.categoryType == incomeCategoryType);
    displayBudgetDataDetails(incomeBudgetData);
    // income summary
    generateBudgetDataSummaryRows(budgetDateDigits, incomeCategoryType, "Total Income: ", 'I');

    // expense
    const expenseBudgetData = budgetDetailsData.filter(i => i.categoryType == expenseCategoryType);
    displayBudgetDataDetails(expenseBudgetData);
    // expense summary
    generateBudgetDataSummaryRows(budgetDateDigits, expenseCategoryType, "Total Expense: ", 'E');

    // net
    generateBudgetDataSummaryRows(budgetDateDigits, "", "Net Income:", 'N');

}

// Generates the sum as per type/group of categories per month
const getSumOfICategoryTypePerMonth = function (categoryType, budgetDateDigit) {

    const budgetDetailsDataSummaryJSON = budgetDetailsDataJSON.filter(b => b.categoryType == categoryType && b.budgetDateDigits == budgetDateDigit);

    let sum = 0;

    budgetDetailsDataSummaryJSON.forEach((item) => {

        sum += Number(item.budgetAmount ?? "0");
    });

    return sum.toFixed(2);

}

// Updates the Net summary only
const getNetSummary = function (budgetDateDigit) {

    const incomeBudgetSum = Number(document.getElementById('I' + budgetDateDigit).textContent.substring(1) ?? 0);
    const expenseBudgetSum = Number(document.getElementById('E' + budgetDateDigit).textContent.substring(1) ?? 0);

    return (incomeBudgetSum - expenseBudgetSum).toFixed(2);

}

// Updates the Income/Expense summary and Net
const updateBudgetSummary = function (categoryType, budgetDateDigit) {

    // Change the Income or Expense Summary
    let tdElement = document.getElementById(`${categoryType == "1" ? "I" : "E"}${budgetDateDigit}`);
    tdElement.textContent = `${userCurrencyDetailsJSON.currencySymbol}${getSumOfICategoryTypePerMonth(categoryType, budgetDateDigit)}`;

    document.getElementById('N' + budgetDateDigit).textContent = `${userCurrencyDetailsJSON.currencySymbol}${getNetSummary(budgetDateDigit)}`;
}

// Save Current Record or Add New Record!!
const saveBudgetRecord = function (record) {

    record.budgetAmount = Number(record.budgetAmount);     // Change data type to number

    fetch("/Budget/SaveBudget", {
        method: "POST",
        credentials: "include",   // REQUIRED for antiforgery cookie
        body: JSON.stringify(record),
        headers: {
            "Content-Type": "application/json",
            "RequestVerificationToken": _token
        }
    })
        .then(res => res.json())
        .then(result => {
            if (result.success) {
                showDbMessage("Budget saved successfully.", true);
            }
            else {
                console.error(result.message);
                showDbMessage("Error: Budget has not been saved!!!", false);
                return;
            }

        });

}


// Delete the Budget Record!!!
const deleteBudgetRecord = function (record) {

    record.budgetAmount = Number(record.budgetAmount);     // Change data type to number to match the model at middle layer

    fetch('/Budget/DeleteBudget', {
        method: 'POST',
        credentials: "include",   // REQUIRED for antiforgery cookie
        headers: {
            "Content-Type": "application/json",
            "RequestVerificationToken": _token
        },
        body: JSON.stringify(record)
    })
        .then(response => {
            // Check if the HTTP status code is 200-299 (Ok)
            if (!response.ok) {
                // throw new Error(`HTTP error! Status: ${response.status}`);
                showDbMessage("Budget has not been deleted successfully!!!", true);
            }
            // Parse the body text as JSON
            return response.json();
        })
        .then(data => {
            showDbMessage("Budget has been deleted.", true);
        })
        .catch(error => {
            console.error("Fetch operation failed:", error);
        });

}

document.getElementById('budgetDetailsStartDate').value = budgetStartDate;
document.getElementById('budgetDetailsEndDate').value = budgetEndDate;

getBudgetDetails(budgetStartDate, budgetEndDate);

// Any element on the modal has focus, will be blurred
$('#addEditBudgetSettingsModal').on('hide.bs.modal', function () {
    if (this.contains(document.activeElement)) {
        document.activeElement.blur();
    }
});

// Note: The unobtrusive validation only shows the error messages unless form's submit validation has been implemented for othen than standard validations...
const refreshBudgetData = function () {

    if (isValidDate(budgetStartDate) && isValidDate(budgetEndDate) && (budgetEndDate >= budgetStartDate) && budgetFiltersChanged) {
        getBudgetDetails(budgetStartDate, budgetEndDate);
        budgetFiltersChanged = false;
    }
}

// Fires when the DOM is loaded completely!!!
document.addEventListener('DOMContentLoaded', () => {

    const filterForm = document.getElementById('budgetDataFilterForm');
    const filterStartDate = document.getElementById('budgetDetailsStartDate');
    const filterEndDate = document.getElementById('budgetDetailsEndDate');

    // blur event listner for all the input [type = number] in budget data table
    document.addEventListener('blur', function (event) {
        const input = event.target;

        if (input.tagName === 'INPUT' && input.type === 'number') {
            let budgetIdAndDate = input.id.split("-");
            let index = budgetDetailsDataJSON.findIndex(d => d.budgetId == budgetIdAndDate[0] && d.budgetDateDigits == budgetIdAndDate[1]);
            let currentBudgetRecord = budgetDetailsDataJSON[index];
            // UPDATE DATABASE
            if (((currentBudgetRecord.budgetAmount ?? "") != input.value) || (currentBudgetRecord.budgetAmount == "0" && input.value == "")) {
                if (input.value == "") {
                    // Delete the record
                    deleteBudgetRecord(currentBudgetRecord);
                    currentBudgetRecord.budgetAmount = null;      // Updates the 'budgetDetailsDataJSON' as well
                }
                else {

                    currentBudgetRecord.budgetAmount = String(input.value); // Updates the 'budgetDetailsDataJSON' as well, must be of type "String" as other value
                    // Save Record
                    saveBudgetRecord(currentBudgetRecord);
                }
                // UPDATE SUMMARY AT CLIENT
                updateBudgetSummary(currentBudgetRecord.categoryType, currentBudgetRecord.budgetDateDigits);
            }

        }
    }, true); // capture phase

    // Shrink & Expand Filter Region
    document.getElementById('showHideBudgetFilters').addEventListener('click', function (event) {
        const containerDiv = document.getElementById('budgetDataFilterDiv');
        if (this.innerText == 'call_received') {
            filterForm.style.display = 'block';
            containerDiv.style.minHeight = '150px';
            this.innerText = 'call_made';
            this.title = 'Hide Filter';
        }
        else {
            filterForm.style.display = 'none';
            containerDiv.style.minHeight = '50px';
            this.innerText = 'call_received';
            this.title = 'Show Filter';
        }
    });

    filterStartDate.addEventListener('change', function () {
        budgetStartDate = this.value;
        budgetFiltersChanged = true;
    });

    filterStartDate.addEventListener('keydown', function (event) {
        if (event.key === 'Enter' && this.value !== '' && this.checkValidity()) {
            event.preventDefault(); // stops the date picker from reopening
            refreshBudgetData();
        }
    });

    filterEndDate.addEventListener('change', function () {
        budgetEndDate = this.value;
        budgetFiltersChanged = true;
    });

    filterEndDate.addEventListener('keydown', function (event) {
        if (event.key === 'Enter' && this.value !== '' && this.checkValidity()) {
            event.preventDefault(); // stops the date picker from reopening
            refreshBudgetData();
        }
    });

});

// Add new data-validation check functions
$.validator.addMethod("lessThan", function (value, element, params) {
    if (!value) return true;

    var targetValue = $(params).val();
    if (!targetValue) return true;

    var startParts = value.split('-');
    var endParts = targetValue.split('-');

    var startDate = new Date(startParts[0], startParts[1] - 1, startParts[2]);
    var endDate = new Date(endParts[0], endParts[1] - 1, endParts[2]);

    return startDate <= endDate;
}, "Start date must be less than or equal to end date.");

$.validator.unobtrusive.adapters.add("lessThan", ["other"], function (options) {
    options.rules["lessThan"] = "#" + options.params.other;
    options.messages["lessThan"] = options.message;
});

// Start Date & End Date Validations
$(document).ready(function () {
    var $form = $("#budgetDataFilterForm");
    // Enable validation on blur
    $form.data("validator").settings.onfocusout = function (element) {
        $(element).valid();
    };
    // Force validation to run IMMEDIATELY when a date input changes
    $("#budgetDetailsStartDate, #budgetDetailsEndDate").on("change blur", function () {
        $('#budgetDetailsStartDate').valid();
        $('#budgetDetailsEndDate').valid();
    });
});
