/*
    EDIT Mode: is not maintained in this module for the same record because multiple records are inserted/updated/deleted in a single request. 
               And their new values  must be retrieved from the database.
*/

let _token = getSecurityToken('#transactionDataFilterForm-1');
let transactionsPaginationJSON = '';
let userCurrencyDetailsJSON = '';

let transactionFilterJSON = {
    transactionFilterStartDate: formatDateToYYYYMMDD(new Date(new Date().getFullYear(), 0, 1)),
    transactionFilterEndDate: formatDateToYYYYMMDD(new Date(new Date().getFullYear(), 11, 31)),
    transactionFilterCategoryId: null,
    transactionFilterAccountId: null,
    transactionFilterDescription: '',
    transactionFilterTagId: null,
    transactionFilterGoalId: null,
    transactionFilterCategorization: 0,
    transactionFilterType: 0
};

let categoriesListJSON = '';
let accountsListJSON = '';
let tagsListJSON = '';
let goalsListJSON = '';
let transactionCategorizationListJSON = '';
let transactionTypeListJSON = '';
let transactionRepeatIntervalListJSON = '';
let transactionsDataMode = '';

let transactionFilterForm1 = document.getElementById('transactionDataFilterForm-1');
let transactionFilterForm2 = document.getElementById('transactionDataFilterForm-2');


// Populates the selects of filter section on the transaction page.
const populateFilterSelects = function () {

    populateSelectFromJSON('transactionFilterCategoryId', categoriesListJSON, 'listOptionId', 'listOptionName', '-- Clear Category Filter --', false);
    populateSelectFromJSON('transactionFilterAccountId', accountsListJSON, 'listOptionId', 'listOptionName', '-- Clear Account Filter --', false);
    populateSelectFromJSON('transactionFilterTagId', tagsListJSON, 'listOptionId', 'listOptionName', '-- Clear Tag Filter --', false);
    populateSelectFromJSON('transactionFilterGoalId', goalsListJSON, 'listOptionId', 'listOptionName', '-- Clear Goal Filter --', false);

    populateSelectFromJSON('transactionFilterCategorizationId', transactionCategorizationListJSON, 'listOptionId', 'listOptionName');
    populateSelectFromJSON('transactionFilterTypeId', transactionTypeListJSON, 'listOptionId', 'listOptionName');

}

// Set the values of Filter Forms to query transaction data!!!
const setTransactionFilterFormValues = function () {

    transactionFilterForm1["transactionFilterStartDate"].value = transactionFilterJSON["transactionFilterStartDate"];
    transactionFilterForm1["transactionFilterEndDate"].value = transactionFilterJSON["transactionFilterEndDate"];
    transactionFilterForm1["transactionFilterCategoryId"].value = transactionFilterJSON["transactionFilterCategoryId"] == null ? "" : transactionFilterJSON["transactionFilterCategoryId"];
    transactionFilterForm1["transactionFilterAccountId"].value = transactionFilterJSON["transactionFilterAccountId"] == null ? "" : transactionFilterJSON["transactionFilterAccountId"];
    transactionFilterForm1["transactionFilterDescription"].value = transactionFilterJSON["transactionFilterDescription"] == null ? "" : transactionFilterJSON["transactionFilterDescription"];
    
    transactionFilterForm2["transactionFilterTagId"].value = transactionFilterJSON["transactionFilterTagId"] == null ? "" : transactionFilterJSON["transactionFilterTagId"];
    transactionFilterForm2["transactionFilterGoalId"].value = transactionFilterJSON["transactionFilterGoalId"] == null ? "" : transactionFilterJSON["transactionFilterGoalId"];
    transactionFilterForm2["transactionFilterCategorization"].value = transactionFilterJSON["transactionFilterCategorization"] == null ? "" : transactionFilterJSON["transactionFilterCategorization"];
    transactionFilterForm2["transactionFilterType"].value = transactionFilterJSON["transactionFilterType"] == null ? "" : transactionFilterJSON["transactionFilterType"];
    
    // To match the UI Validation Behavior Consistent;
    $("[name='transactionFilterStartDate']").data("focused", true); 
    $("[name='transactionFilterEndDate']").data("focused", true); 

}

// Sets the filter JSON data to retrieve filtered transaction data
const setTransactionFilterJSONValues = function () {

    transactionFilterJSON["transactionFilterStartDate"] = transactionFilterForm1["transactionFilterStartDate"].value;
    transactionFilterJSON["transactionFilterEndDate"] = transactionFilterForm1["transactionFilterEndDate"].value;
    transactionFilterJSON["transactionFilterCategoryId"] = transactionFilterForm1["transactionFilterCategoryId"].value == "" ? null : transactionFilterForm1["transactionFilterCategoryId"].value;
    transactionFilterJSON["transactionFilterAccountId"] = transactionFilterForm1["transactionFilterAccountId"].value == "" ? null : transactionFilterForm1["transactionFilterAccountId"].value;
    transactionFilterJSON["transactionFilterDescription"] = transactionFilterForm1["transactionFilterDescription"].value ? transactionFilterForm1["transactionFilterDescription"].value.trim() : null;

    transactionFilterJSON["transactionFilterTagId"] = transactionFilterForm2["transactionFilterTagId"].value == "" ? null : transactionFilterForm2["transactionFilterTagId"].value;
    transactionFilterJSON["transactionFilterGoalId"] = transactionFilterForm2["transactionFilterGoalId"].value == "" ? null : transactionFilterForm2["transactionFilterGoalId"].value;
    transactionFilterJSON["transactionFilterCategorization"] = +(transactionFilterForm2["transactionFilterCategorization"].value || 0);
    transactionFilterJSON["transactionFilterType"] = +(transactionFilterForm2["transactionFilterType"].value || 0);

}


// Displays the data in the table in UI (<tbody id="transactionsTBody">)...
const displayTransactionData = function (transactionsData, pagination) {
    // Select the table body element
    const tbody = document.getElementById("transactionsTBody");

    // Clear existing contents safely
    tbody.innerHTML = "";

    // Iterate over the new data and append new rows
    transactionsData.forEach(item => {
        // Create a new row element
        const row = document.createElement("tr");
        //data-secure
        row.setAttribute("data-secure-index", item.transactionId);

        // Populate the row with specific cell data
        row.innerHTML = `<td>${formatToLocalDate(item.transactionDate, 'en-US')}</td>
            <td style="white-space: normal; word-break: break-word; width: 30% !important;">${item.transactionDescription.trim()}${item.tagId !== null ? `&nbsp;<span class="position-relative top-0 translate-middle badge rounded-pill bg-secondary material-symbols-outlined-panel-pill" title="${tagsListJSON.find(t => t.listOptionId == item.tagId)?.listOptionName}")>new_label</span>` : ''}</td>
            <td style="text-align: right;">${item.goalId !== null ? `<span style="margin-right: 0px !important;" class="position-relative top-0 translate-middle badge rounded-pill bg-secondary material-symbols-outlined-panel-pill" title="${goalsListJSON.find(t => t.listOptionId == item.goalId)?.listOptionName}">money_range</span>` : ''}${userCurrencyDetailsJSON["currencySymbol"] + item.transactionAmount}</td>
            <td>${accountsListJSON.find(t => t.listOptionId == item.accountId)?.listOptionName}</td>
            <td>${categoriesListJSON.find(t => t.listOptionId == item.categoryId)?.listOptionName}</td>
            <td>${item.transactionType == 2 ? '<span class="badge bg-secondary">Recurring</span>' :  ''}</td>
            <td><span title="Edit" class="material-symbols-outlined-data-icon">edit</span></td>
            <td><span title="Delete" class="material-symbols-outlined-data-icon">delete</span></td>`;

        // Append the complete row to the table body
        tbody.appendChild(row);
    });

    const row = getPaginationRow(pagination, 8);
    tbody.appendChild(row);
}

// Generates/Creates a new modal and shows or hide the same modal...
const showHideAddEditTransactionModal = function (show) {

    const addEditTransactionModalElement = document.getElementById('addEditTransactionModal');

    // Get the existing instance, or create a new one if it doesn't exist yet
    let modalInstance = bootstrap.Modal.getInstance(addEditTransactionModalElement);

    if (!modalInstance) {
        modalInstance = new bootstrap.Modal(addEditTransactionModalElement);
    }

    if (show) {

        if (transactionsDataMode == 'Add') {
            document.getElementById('addEditTransactionModalLabelId').innerHTML = '<span class="material-symbols-outlined-data-icon">add</span> Add Transaction';
            showHideTransactionRepeatContainerDiv(true);   // Show the transaction repeat details in 'Add' mode.
        }
        if (transactionsDataMode == 'Edit') {
            document.getElementById('addEditTransactionModalLabelId').innerHTML = '<span class="material-symbols-outlined-data-icon">edit</span> Edit Transaction';
            showHideTransactionRepeatContainerDiv(false);  // Hide the transaction repeat details in 'Edit' mode.
        }

        modalInstance.show();

    } else {

        modalInstance.hide();
    }

}

// Display the modal with Data
const showAddEditTransactionModal = function (record) {

    populateSelectFromJSON('modalAccountId', accountsListJSON, 'listOptionId', 'listOptionName', '-- Select Account --');
    populateSelectFromJSON('modalCategoryId', categoriesListJSON, 'listOptionId', 'listOptionName', '-- Select Category --');
    populateSelectFromJSON('modalGoalId', goalsListJSON, 'listOptionId', 'listOptionName', '-- Select Goal --', false);
    populateSelectFromJSON('modalTagId', tagsListJSON, 'listOptionId', 'listOptionName', '-- Select Tag --',  false);
    populateSelectFromJSON('modalTransactionRepeatInterval', transactionRepeatIntervalListJSON, 'listOptionId', 'listOptionName');
    
    document.getElementById("modalIsTransactionRepeatActive").checked = record.isTransactionRepeatActive == 1;
    document.getElementById("transactionRepeatFieldsContainer").style.display = record.isTransactionRepeatActive == 0 ? "none" : "block";
    
    // Display the currency symbol in the modal
    document.getElementById('currencySymbolId').innerText = userCurrencyDetailsJSON["currencySymbol"];
    
    setFormData('addEditTransactionForm', record);
    
    // Open up the Bootstrap modal visually
    showHideAddEditTransactionModal(true);
}


// Show or Hide Transaction Repeat Details in Modal
const showHideTransactionRepeatContainerDiv = function (show) {
    transactionRepeatContainerDiv = document.getElementById('transactionRepeatFieldsContainerDiv');
    transactionRepeatContainerDiv.style.display = show ? 'block' : 'none';
}

// Retrives the data AFTER 1st ever request 
const doNavigation = function (event, navigationRequest, firstRequest) {

    if (event) {
        event.preventDefault();
    }

    transactionsPaginationJSON = firstRequest ? "" : getNewRecordStartAndEndNumber(transactionsPaginationJSON, navigationRequest);  // First request values will be set at post request regardless!!!

    const payload = {
        pagination: transactionsPaginationJSON,
        transactionFilter: transactionFilterJSON,
        firstRequest: firstRequest
    };

    fetch('/transactions', {
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
                categoriesListJSON = data.categoriesList;
                accountsListJSON = data.accountsList;
                tagsListJSON = data.tagsList;     
                goalsListJSON = data.goalsList;
                transactionCategorizationListJSON = data.transactionCategorizationList;
                transactionTypeListJSON = data.transactionTypeList;
                transactionRepeatIntervalListJSON = data.transactionRepeatIntervalList;
                transactionsPaginationJSON = data.pagination;
                userCurrencyDetailsJSON = data.userCurrencyDetails;
                populateFilterSelects();
                setTransactionFilterFormValues();
            }

            // Work with your parsed data object here
            displayTransactionData(data.transactions, transactionsPaginationJSON);
        })
        .catch(error => {
            console.error("Fetch operation failed:", error);
        });

}

// Retrieve single record
const getTransactionRecord = function (transactionId) {

    fetch('/Transaction/GetTransaction', {
        method: 'POST',
        credentials: "include",   // REQUIRED for antiforgery cookie
        headers: {
            "Content-Type": "application/json",
            "RequestVerificationToken": _token
        },
        body: JSON.stringify({ transactionId: String(transactionId) })
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
            rebindValidation($("#addEditTransactionForm"));
            // Work with parsed data object here
            data.transactionRepeatInterval = data.transactionRepeatInterval || 3; // Default to "Monthly" in 'Edit' mode. * REQUIRED FIELD
            showAddEditTransactionModal(data);
        })
        .catch(error => {
            console.error("Fetch operation failed:", error);
        });
}

// Save Current Record or Add New Record!!
const saveTransactionRecord = function () {

    var data = getFormEntries('addEditTransactionForm');
    data.transactionAmount = Number(data.transactionAmount); // Convert String to a Number
    data.transactionCategorization = data.transactionCategorization ? Number(data.transactionCategorization) : 1;
    data.transactionType = data.transactionType ? Number(data.transactionType) : 1;
    data.isTransactionRepeatActive = Number(data.isTransactionRepeatActive);
    data.transactionRepeatId = Number(data.transactionRepeatId);    // blank will be converted to 0.

    if(data.isTransactionRepeatActive == 0){
        data.transactionRepeatInterval = null;
        data.transactionRepeatEndDate = null;
    }
    else{
        
        data.transactionRepeatInterval = Number(data.transactionRepeatInterval);
        data.transactionRepeatEndDate = data.transactionRepeatEndDate || null;      // save the date value as is from the form's input type=date or null... correct way!!!
    }

    fetch("/Transaction/SaveTransaction", {
        method: "POST",
        credentials: "include",   // REQUIRED for antiforgery cookie
        body: JSON.stringify(data),
        headers: {
            "Content-Type": "application/json",
            "RequestVerificationToken": _token
        }
    })
        .then(res => res.json())
        .then(result => {
            if (result.success) {
                showHideAddEditTransactionModal(false);
                doNavigation(null, 'current');  // Refresh the page!
                showDbMessage("Transaction saved successfully.", true);
            }
            else {
                console.error(result.message);
                showDbMessage("Error: Transaction has not been saved successfully!!!", false);
                return;
            }

        });

}

// Delete the Transaction Record!!!
const deleteTransactionRecord = function (transactionId) {

    fetch('/Transaction/DeleteTransaction', {
        method: 'POST',
        credentials: "include",   // REQUIRED for antiforgery cookie
        headers: {
            "Content-Type": "application/json",
            "RequestVerificationToken": _token
        },
        body: JSON.stringify({ transactionId: String(transactionId) })
    })
        .then(response => {
            // Check if the HTTP status code is 200-299 (Ok)
            if (!response.ok) {
                // throw new Error(`HTTP error! Status: ${response.status}`);
                showDbMessage("Transaction has not been deleted successfully!!!", true);
            }
            // Parse the body text as JSON
            return response.json();
        })
        .then(data => {
            doNavigation(null, 'current');  // Refresh the page!
            showDbMessage("Transaction deleted successfully.", true);
        })
        .catch(error => {
            console.error("Fetch operation failed:", error);
        });

}

// Shrink & Expand Filter Region
document.getElementById('showHideTransactionFilters').addEventListener('click', function (event) {

    const containerDiv = document.getElementById('transactionDataFilterDiv');

    if (this.innerText == 'call_received') {    // Expanded Mode
        transactionFilterForm2.style.display = 'flex';
        containerDiv.style.minHeight = '150px';
        this.innerText = 'call_made';
        this.title = 'Shrink Filter';
    }
    else {      // Shrinked Mode

        transactionFilterForm2.style.display = 'none';
        containerDiv.style.minHeight = '100px';
        this.innerText = 'call_received';
        this.title = 'Expand Filter';
    }
});


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
        transactionsDataMode = actionType;    // Edit and Delete will be confirmed here

        // Execute specific logic depending on what was clicked
        if (actionType === "Edit") {

            getTransactionRecord(recordId);

        } else if (actionType === "Delete") {
            if (confirm('Do you want to delete the transaction?')) {
                deleteTransactionRecord(recordId);
            }
        }

    }

});

// Add new transaction record
document.querySelector('span[title="Add"]').addEventListener('click', function (event) {
    transactionsDataMode = 'Add';
    clearFormData('#addEditTransactionForm');
    rebindValidation($("#addEditTransactionForm"));
    const record = getFormEntries('addEditTransactionForm');    // Retrieve a blank record!!!

    record.transactionCategorization = 1; // Manual Entry
    record.transactionType =  1;    // Regular Transaction
    record.transactionRepeatInterval = 3; // Default to "Monthly" in 'Add' mode. * REQUIRED FIELD

    showAddEditTransactionModal(record);
});

// Change the title of the select
document.getElementById("transactionDataFilterDiv").addEventListener("change", e => {
    if (e.target.tagName === "SELECT") {
        e.target.title = e.target.options[e.target.selectedIndex].text;
    }
});

// Show or Hide Transaction Repeat Details:
document.getElementById("modalIsTransactionRepeatActive").addEventListener("change", e => {
    e.preventDefault();
    const isChecked = e.target.checked;
    e.target.value = isChecked ? 1 : 0;
    document.getElementById("transactionRepeatFieldsContainer").style.display = isChecked ? "block" : "none";
});

// Requery transaction data - Start
const refreshTransactionData = function(formId) {

    setTransactionFilterJSONValues();       // Filter values are changed only here!!!
    if (isValidForm(formId)) {
        doNavigation(null, 'first', true);
    }
    
}

document.getElementById("viewTransactionData").addEventListener("click", (e) => {
    
    e.preventDefault();
    refreshTransactionData('transactionDataFilterForm-1');
    
});

document.getElementById("transactionDataFilterDiv").addEventListener("keypress", (e) => {

    if ((e.target.tagName === "INPUT" || e.target.tagName === "SELECT") && e.key === "Enter") {
        e.preventDefault();
        refreshTransactionData('transactionDataFilterForm-1');
    }
});

// Requery transaction data - End

//  Set the Category Type value for the hidden field to validate amount!
document.getElementById('modalCategoryId').addEventListener("change", (e) => {

    e.preventDefault();
    document.getElementById('modalCategoryType').value = categoriesListJSON.find(c => c.listOptionId == e.target.value)?.listOptionType;

});

// Load the first time data!!!
doNavigation(null, 'first', true);


// The date must be greater than or equal to today's date. This validation is applied only when the checkbox is checked.
// Register the comparison logic
$.validator.addMethod("dategte", function (value, element) {
  const checkbox = $("#modalIsTransactionRepeatActive");
  
  // If checkbox is not checked or input is empty, validation passes
  if (!checkbox.is(":checked") || !value) {
    return true;
  }

  // Parse YYYY-MM-DD in local timezone
  const parts = value.split('-');
  const inputDate = new Date(parts[0], parts[1] - 1, parts[2]);

  const today = new Date();
  today.setHours(0, 0, 0, 0);

  return inputDate >= today;
});

// Register with Unobtrusive Validation
$.validator.unobtrusive.adapters.addBool("dategte");

// Validate Amount Value for Category!!
appendAmountValidationForCategoryType( 'categoryId' , 'transactionAmount' );

// Start Date must be lower than End Date
appendLessThanDateValueValidationFunctionality();

// Configure Unobtrusive Validations Settings On Modal and it's Form (Must be last always)
setUpUnobtrusiveValidationOnModal('#addEditTransactionModal');

// Save the transaction record
$(document).ready(function () {

    // Re-evaluate date when checkbox changes
    $("#modalIsTransactionRepeatActive").on("change", function () {
        $("#modalTransactionDate").valid();
    });

    // Re-evaluate date on direct change or input
    $("#modalTransactionDate").on("change input", function () {
        $(this).valid();
    });

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

