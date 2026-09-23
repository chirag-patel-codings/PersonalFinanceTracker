let _token = getSecurityToken('#addEditTransactionRuleForm');
let transactionRulesPaginationJSON = '';
let userCurrencyDetailsJSON = '';

let accountsListJSON = '';
let categoriesListJSON = '';
let goalsListJSON = '';
let transactionTextMatchComparisionListJSON = '';
let transactionAmountMatchComparisionListJSON = '';
let transactionRulesDataMode = '';
let prevQueriedRecordId = '';


// Displays the data in the table in UI (<tbody id="transactionRulesTBody">)...
const displayTransactionRulesData = function (transactionRulesData, pagination) {
    // Select the table body element
    const tbody = document.getElementById("transactionRulesTBody");

    // Clear existing contents safely
    tbody.innerHTML = "";

    // Iterate over the new data and append new rows
    transactionRulesData.forEach(item => {
        // Create a new row element
        const row = document.createElement("tr");
        //data-secure
        row.setAttribute("data-secure-index", item.transactionRuleId);

        // Populate the row with specific cell data
        row.innerHTML = `<td>${item.accountId ? accountsListJSON.find(t => t.listOptionId == item.accountId)?.listOptionName : 'All Accounts'}</td>
            <td>${transactionTextMatchComparisionListJSON.find(t => t.listOptionId == item.transactionTextMatchComparision)?.listOptionName}</td>
            <td style="white-space: normal; word-break: break-word; width: 30% !important;">${item.transactionTextToMatch}</td>
            <td>${categoriesListJSON.find(t => t.listOptionId == item.categoryId)?.listOptionName}</td>
            <td>${item.goalId !== null ? goalsListJSON.find(t => t.listOptionId == item.goalId)?.listOptionName : '-'}</td>
            <td>${item.transactionDescriptionOverride == null ? '-' : item.transactionDescriptionOverride}</td>
            <td><span title="Edit" class="material-symbols-outlined-data-icon">edit</span></td>
            <td><span title="Delete" class="material-symbols-outlined-data-icon">delete</span></td>`;

        // Append the complete row to the table body
        tbody.appendChild(row);
    });

    const row = getPaginationRow(pagination, 8);
    tbody.appendChild(row);
}

// Dynamically show or hide the transaction amount input field based on the selected value in the "Transaction Amount Match Comparison" dropdown. 
// If the user selects "Exact Amount" (value 3), the input field will be displayed; otherwise, it will be hidden.
const showHideTransactionAmountInput = function (value) {
    document.getElementById("exactTransactionAmountContainerDiv").style.display = value == 3 ? "block" : "none"; // Enable only if "Exact Amount" is selected
}

// Displays the functional text in the modal based on the current selections and inputs made by the user.
const displayTransactionRuleFunctionalText = function () {

    const modalAccountId = document.getElementById('modalAccountId');
    const modalTransactionTextMatchComparision = document.getElementById('modalTransactionTextMatchComparision');
    const modalTransactionTextToMatch = document.getElementById('modalTransactionTextToMatch');
    const modalTransactionAmountMatchComparision = document.getElementById('modalTransactionAmountMatchComparision');
    const modalTransactionExactAmount = document.getElementById('modalTransactionExactAmount');
    const trnRuleFunctionalDescription = document.getElementById('trnRuleFunctionalDescription');
    const modalCategoryId = document.getElementById('modalCategoryId');
    const modalGoalId = document.getElementById('modalGoalId');
    const modalTransactionDescriptionOverride = document.getElementById('modalTransactionDescriptionOverride');

    let modalAccountIdText = modalAccountId.options[modalAccountId.selectedIndex].text;
    let modalAccountIdDisplayText = modalAccountId.value != 0 ? ' for the <b><i>' + modalAccountIdText + '</i> Account</b> ' : '';
    let modalTransactionTextMatchComparisionText = ' ' + modalTransactionTextMatchComparision.options[modalTransactionTextMatchComparision.selectedIndex].text;
    let modalTransactionTextToMatchDisplayValue = ` ${modalTransactionTextToMatch.value.trim() == '' ? 'your match text' : modalTransactionTextToMatch.value.trim()}`;
    let modalTransactionAmountMatchComparisionText = modalTransactionAmountMatchComparision.options[modalTransactionAmountMatchComparision.selectedIndex].text;
    let modalTransactionExactAmountValue = modalTransactionExactAmount.value.trim() == '' ? '0.00' : modalTransactionExactAmount.value.trim();
    let modalTransactionAmountMatchComparisionDisplayText = modalTransactionAmountMatchComparision.value != 0 ? (modalTransactionAmountMatchComparision.value != 3 ? ', and it is <b>' + modalTransactionAmountMatchComparisionText + '</b>': ', and the <b>Exact Amount</b> is <i>' + modalTransactionExactAmountValue + '</i>') : '';
    let modalCategoryIdText = modalCategoryId.options[modalCategoryId.selectedIndex].text;
    let modalCategoryIdDisplayText = `, then assign<b><i>${modalCategoryId.value != 0 ? ' ' + modalCategoryIdText : ''}</i></b> Category`;
    let modalGoalIdText = modalGoalId.options[modalGoalId.selectedIndex].text;
    let modalGoalIdDisplayText = `${modalGoalId.value != 0 ? ', and <b><i>' + modalGoalIdText + '</i></b> Goal' : ''}`;
    let modalTransactionDescriptionOverrideValue = modalTransactionDescriptionOverride.value.trim() == '' ? '' : ', and change the description to <i>' + modalTransactionDescriptionOverride.value.trim() + '</i>';

    let ruleFunctionalDescription = `If the description${modalAccountIdDisplayText}<b>${modalTransactionTextMatchComparisionText}</b><i>${modalTransactionTextToMatchDisplayValue}</i>${modalTransactionAmountMatchComparisionDisplayText}${modalCategoryIdDisplayText}${modalGoalIdDisplayText}${modalTransactionDescriptionOverrideValue}.`;
    
    trnRuleFunctionalDescription.innerHTML = ruleFunctionalDescription;

}

// Generates/Creates a new modal and shows or hide the same modal...
const showHideAddEditTransactionRulesModal = function (show) {

    const addEditTransactionRuleModalElement = document.getElementById('addEditTransactionRuleModal');

    // Get the existing instance, or create a new one if it doesn't exist yet
    let modalInstance = bootstrap.Modal.getInstance(addEditTransactionRuleModalElement);

    if (!modalInstance) {
        modalInstance = new bootstrap.Modal(addEditTransactionRuleModalElement);
    }

    if (show) {

        if (transactionRulesDataMode == 'Add') {
            document.getElementById('addEditTransactionRuleModalLabelId').innerHTML = '<span class="material-symbols-outlined-data-icon">add</span> Add Transaction Rule';
        }
        if (transactionRulesDataMode == 'Edit') {
            document.getElementById('addEditTransactionRuleModalLabelId').innerHTML = '<span class="material-symbols-outlined-data-icon">edit</span> Edit Transaction Rule';
        }

        modalInstance.show();

    } else {

        modalInstance.hide();
    }

}

// Display the modal with Data
const showAddEditTransactionRulesModal = function (record) {

    if (record) {
        populateSelectFromJSON('modalAccountId', accountsListJSON, 'listOptionId', 'listOptionName', 'All Accounts', false);
        populateSelectFromJSON('modalTransactionTextMatchComparision', transactionTextMatchComparisionListJSON, 'listOptionId', 'listOptionName');
        populateSelectFromJSON('modalTransactionAmountMatchComparision', transactionAmountMatchComparisionListJSON, 'listOptionId', 'listOptionName');

        populateSelectFromJSON('modalCategoryId', categoriesListJSON, 'listOptionId', 'listOptionName', '-- Select Category --');
        populateSelectFromJSON('modalGoalId', goalsListJSON, 'listOptionId', 'listOptionName', '-- Select Goal --', false);
        
        // Display the currency symbol in the modal
        document.getElementById('transactionCurrencySymbolId').innerText = userCurrencyDetailsJSON["currencySymbol"];

        // Show or Hide the Transaction Amount Input based on the value
        showHideTransactionAmountInput(record.transactionAmountMatchComparision);
        
        setFormData('addEditTransactionRuleForm', record);

        // Display the rule description in the modal
        displayTransactionRuleFunctionalText();
    }

    // Open up the Bootstrap modal visually
    showHideAddEditTransactionRulesModal(true);

}

// Retrives the data AFTER 1st ever request 
const doNavigation = function (event, navigationRequest, firstRequest) {

    if (event) {
        event.preventDefault();
    }

    transactionRulesPaginationJSON = firstRequest ? "" : getNewRecordStartAndEndNumber(transactionRulesPaginationJSON, navigationRequest);  // First request values will be set at post request regardless!!!

    const payload = {
        pagination: transactionRulesPaginationJSON,
        firstRequest: firstRequest
    };

    fetch('/transactions/rules', {
        method: 'POST',
        credentials: "include",
        headers: {
            "Content-Type": "application/json",
            "RequestVerificationToken": _token
        },
        body: JSON.stringify(payload)
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
            if (firstRequest) {
                accountsListJSON = data.accountsList;
                categoriesListJSON = data.categoriesList;
                goalsListJSON = data.goalsList;
                transactionTextMatchComparisionListJSON = data.transactionTextMatchComparisionList;
                transactionAmountMatchComparisionListJSON = data.transactionAmountMatchComparisionList;
                transactionRulesPaginationJSON = data.pagination;
                userCurrencyDetailsJSON = data.userCurrencyDetails;
            }

            // Work with your parsed data object here
            displayTransactionRulesData(data.transactionRules, transactionRulesPaginationJSON);
        })
        .catch(error => {
            console.error("Fetch operation failed:", error);
        });

}

// Retrieve single record
const getTransactionRuleRecord = function (transactionRuleId) {

    fetch('/TransactionRule/GetTransactionRule', {
        method: 'POST',
        credentials: "include",   // REQUIRED for antiforgery cookie
        headers: {
            "Content-Type": "application/json",
            "RequestVerificationToken": _token
        },
        body: JSON.stringify({ transactionRuleId: String(transactionRuleId) })
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
            rebindValidation($("#addEditTransactionRuleForm"));
            // Work with parsed data object here
            showAddEditTransactionRulesModal(data);
        })
        .catch(error => {
            console.error("Fetch operation failed:", error);
        });
}

// Save Current Record or Add New Record!!
// PENDING: APPLY RULE TO EXISTING TRANSACTIONS!!!
const saveTransactionRuleRecord = function () {

    const modalApplyRuleToExistingTransactions = document.getElementById("modalApplyRuleToExistingTransactions");

    var data = getFormEntries('addEditTransactionRuleForm');
    data.transactionTextMatchComparision = Number(data.transactionTextMatchComparision);
    data.transactionAmountMatchComparision = Number(data.transactionAmountMatchComparision);
    data.transactionExactAmount = data.transactionAmountMatchComparision == 3 ? Number(data.transactionExactAmount) : null; // Convert String to a Number

    const payload = {
        data: data,
        applyRule: modalApplyRuleToExistingTransactions.checked ? 1 : 0
    };

    fetch("/TransactionRule/SaveTransactionRule", {
        method: "POST",
        credentials: "include",   // REQUIRED for antiforgery cookie
        body: JSON.stringify(payload),
        headers: {
            "Content-Type": "application/json",
            "RequestVerificationToken": _token
        }
    })
        .then(res => res.json())
        .then(result => {
            if (result.success) {
                modalApplyRuleToExistingTransactions.checked = false; // Reset the checkbox after saving
                showHideAddEditTransactionRulesModal(false);
                doNavigation(null, 'current');  // Refresh the page!
                showDbMessage("Transaction Rule saved successfully.", true);
            }
            else {
                console.error(result.message);
                showDbMessage("Error: Transaction Rule has not been saved successfully!!!", false);
                return;
            }

        });

}

// Delete the TransactionRules Record!!!
const deleteTransactionRuleRecord = function (transactionRuleId) {

    fetch('/TransactionRule/DeleteTransactionRule', {
        method: 'POST',
        credentials: "include",   // REQUIRED for antiforgery cookie
        headers: {
            "Content-Type": "application/json",
            "RequestVerificationToken": _token
        },
        body: JSON.stringify({ transactionRuleId: String(transactionRuleId) })
    })
        .then(response => {
            // Check if the HTTP status code is 200-299 (Ok)
            if (!response.ok) {
                // throw new Error(`HTTP error! Status: ${response.status}`);
                showDbMessage("Transaction Rule has not been deleted successfully!!!", true);
            }
            // Parse the body text as JSON
            return response.json();
        })
        .then(data => {
            doNavigation(null, 'current');  // Refresh the page!
            showDbMessage("Transaction Rule deleted successfully.", true);
        })
        .catch(error => {
            console.error("Fetch operation failed:", error);
        });

}

// Listen for clicks anywhere inside the table body
document.querySelector('tbody').addEventListener('click', function (event) {

    // Check if the clicked element (or its parent) has a title of "Edit" or "Delete"
    const clickedAction = event.target.closest('span[title="Edit"], span[title="Delete"]');

    // If the click wasn't on one of those spans, ignore it
    if (!clickedAction) return;

    // Prevent any default browser behavior (like anchor tags jumps)
    event.preventDefault();

    // Find the closest table row containing the clicked span
    const row = clickedAction.closest('tr');

    if (row) {
        // Grab the hidden ID from that specific row
        const recordId = row.getAttribute('data-secure-index');

        // Get the encrypted record and display or  process!!!
        const actionType = clickedAction.getAttribute('title');
        transactionRulesDataMode = actionType;    // Edit and Delete will be confirmed here

        // Execute specific logic depending on what was clicked
        if (actionType === "Edit") {
            
            if (recordId === prevQueriedRecordId) {
                showAddEditTransactionRulesModal(null); // Show the existing details
            }
            else {
                getTransactionRuleRecord(recordId);
                prevQueriedRecordId = recordId;
            }

        } else if (actionType === "Delete") {
            if (confirm('Do you want to delete the transaction?')) {
                deleteTransactionRuleRecord(recordId);
            }
        }

    }

});

// Add new transaction record
document.querySelector('span[title="Add"]').addEventListener('click', function (event) {
    prevQueriedRecordId = '';
    transactionRulesDataMode = 'Add';
    clearFormData('#addEditTransactionRuleForm');
    rebindValidation($("#addEditTransactionRuleForm"));
    
    const record = getFormEntries('addEditTransactionRuleForm');    // Retrieve a blank record!!!
    record.transactionTextMatchComparision = 0; // Default to "Contains"
    record.transactionAmountMatchComparision = 0; // Default to "Any Amount"

    showAddEditTransactionRulesModal(record);
});

// Change the title of the select
document.getElementById("addEditTransactionRuleModal").addEventListener("change", e => {
    if (e.target.tagName === "SELECT") {
        e.target.title = e.target.options[e.target.selectedIndex].text;
        if(e.target.id === "modalTransactionAmountMatchComparision") {
            showHideTransactionAmountInput(e.target.value);
        }
    }

    displayTransactionRuleFunctionalText();
});


// Load the first time data!!!
doNavigation(null, 'first', true);

/*


// The field becomes required only when the dropdown value is "3"
$.validator.addMethod("requiredif", function (value, element) {
    const other = $("#modalTransactionAmountMatchComparision").val();

    // If dropdown is "3", the field must not be empty
    if (other === "3") {
        return $.trim(value).length > 0;
    }

    return true; // otherwise pass
});

// Register with unobtrusive validation
$.validator.unobtrusive.adapters.addBool("requiredif");
*/

// Add custom validation method
$.validator.addMethod("requiredif", function (value, element) {
    const other = $("#modalTransactionAmountMatchComparision").val();
    if (other === "3") {
        return $.trim(value).length > 0;
    }
    return true;
});

// Register with unobtrusive validation using 'add' (supports error messages)
$.validator.unobtrusive.adapters.add("requiredif", function (options) {
    options.rules["requiredif"] = true;
    options.messages["requiredif"] = options.message;
});

// Configure Unobtrusive Validations Settings On Modal and it's Form (Must be last always)
setUpUnobtrusiveValidationOnModal('#addEditTransactionRuleModal');

// Save the transaction record
$(document).ready(function () {

    // Check validation on Save click
    $('#btnTransactionRuleModalSaveChanges').on('click', function () {
        const $form = $('#addEditTransactionRuleForm');
        if (!$form.valid()) {
            return;
        }
        console.log("Form is valid! Sending AJAX payload...");
        saveTransactionRuleRecord();

    });

});

