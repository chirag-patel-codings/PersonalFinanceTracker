/*
    EDIT Mode: is not maintained in this module for the same record because multiple records are inserted/updated/deleted in a single request. 
               And their new values  must be retrieved from the database.
*/

let _token = getSecurityToken('#addEditRecurringForm');
let recurringsPaginationJSON = '';
let userCurrencyDetailsJSON = '';

let categoriesListJSON = '';
let accountsListJSON = '';
let tagsListJSON = '';
let goalsListJSON = '';
let transactionRepeatIntervalListJSON = '';
let recurringsDataMode = '';


// Displays the data in the table in UI (<tbody id="recurringsTBody">)...
const displayRecurringData = function (recurringsData, pagination) {
    // Select the table body element
    const tbody = document.getElementById("recurringsTBody");

    // Clear existing contents safely
    tbody.innerHTML = "";

    let recurringStatus = '';
    // Iterate over the new data and append new rows
    recurringsData.forEach(item => {
        // Create a new row element
        const row = document.createElement("tr");
        //data-secure
        row.setAttribute("data-secure-index", item.recurringId);

        recurringStatus = (item.isTransactionRepeatActive == "0" && item.recurringNextDate) ? 'Paused' : !item.recurringNextDate ? 'Completed' : 'Active';
        // Populate the row with specific cell data
        row.innerHTML = `<td style="white-space: normal; word-break: break-word; width: 30% !important;">${item.recurringDescription.trim()}${item.tagId !== null ? `&nbsp;<span class="position-relative top-0 translate-middle badge rounded-pill bg-secondary material-symbols-outlined-panel-pill" title="${tagsListJSON.find(t => t.listOptionId == item.tagId)?.listOptionName}")>new_label</span>` : ''}</td>
            <td style="text-align: right;">${item.goalId !== null ? `<span style="margin-right: 0px !important;" class="position-relative top-0 translate-middle badge rounded-pill bg-secondary material-symbols-outlined-panel-pill" title="${goalsListJSON.find(t => t.listOptionId == item.goalId)?.listOptionName}">money_range</span>` : ''}${userCurrencyDetailsJSON["currencySymbol"] + item.recurringAmount}</td>
            <td>${accountsListJSON.find(t => t.listOptionId == item.accountId)?.listOptionName}${`&nbsp;<span class="position-relative top-0 translate-middle badge rounded-pill bg-secondary material-symbols-outlined-panel-pill" title="${categoriesListJSON.find(t => t.listOptionId == item.categoryId)?.listOptionName}">new_label</span>`}</td>
            <td>${transactionRepeatIntervalListJSON.find(t => t.listOptionId == item.transactionRepeatInterval)?.listOptionName}</td>
            <td>${item.recurringNextDate ? formatToLocalDate(item.recurringNextDate, 'en-US') : ' - '}</td>
            <td>${item.transactionRepeatEndDate ? formatToLocalDate(item.transactionRepeatEndDate, 'en-US') : ' - '}</td>
            <td>${'<span class="badge bg-secondary">' + recurringStatus + '</span>'}</td>
            <td><span title="Edit" class="material-symbols-outlined-data-icon">edit</span></td>
            <td><span title="Delete" class="material-symbols-outlined-data-icon">delete</span></td>`;

        // Append the complete row to the table body
        tbody.appendChild(row);
    });

    const row = getPaginationRow(pagination, 8);
    tbody.appendChild(row);
}

// Generates/Creates a new modal and shows or hide the same modal...
const showHideAddEditRecurringModal = function (show) {

    const addEditRecurringModalElement = document.getElementById('addEditRecurringModal');
    const addEditRecurringModalLabel = document.getElementById('addEditRecurringModalLabelId');
    const deleteRecurringSeriesAnchor = document.getElementById('deleteRecurringSeries');

    // Get the existing instance, or create a new one if it doesn't exist yet
    let modalInstance = bootstrap.Modal.getInstance(addEditRecurringModalElement);

    if (!modalInstance) {
        modalInstance = new bootstrap.Modal(addEditRecurringModalElement);
    }

    if (show) {

        if (recurringsDataMode == 'Add') {
            addEditRecurringModalLabel.innerHTML = '<span class="material-symbols-outlined-data-icon">add</span> Add Transaction Recurring';
            deleteRecurringSeriesAnchor.style.display = 'none'; // Hide the delete series button in Add mode
        }
        if (recurringsDataMode == 'Edit') {
            addEditRecurringModalLabel.innerHTML = '<span class="material-symbols-outlined-data-icon">edit</span> Edit Transaction Recurring';
            deleteRecurringSeriesAnchor.style.display = 'block'; // Show the delete series button in Edit mode
        }

        modalInstance.show();

    } else {

        modalInstance.hide();
    }

}

// Display the modal with Data
const showAddEditRecurringModal = function (record) {

    populateSelectFromJSON('modalAccountId', accountsListJSON, 'listOptionId', 'listOptionName', '-- Select Account --');
    populateSelectFromJSON('modalCategoryId', categoriesListJSON, 'listOptionId', 'listOptionName', '-- Select Category --');
    populateSelectFromJSON('modalGoalId', goalsListJSON, 'listOptionId', 'listOptionName', '-- Select Goal --', false);
    populateSelectFromJSON('modalTagId', tagsListJSON, 'listOptionId', 'listOptionName', '-- Select Tag --',  false);
    populateSelectFromJSON('modalTransactionRepeatInterval', transactionRepeatIntervalListJSON, 'listOptionId', 'listOptionName');
    
    $("[name='recurringNextDate']").data("focused", true); // To match the UI Validation Behavior Consistent;
    $("[name='transactionRepeatEndDate']").data("focused", true); // To match the UI Validation Behavior Consistent;
    
    document.getElementById("modalIsTransactionRepeatActive").checked = record.isTransactionRepeatActive == 1;
    
    // Display the currency symbol in the modal
    document.getElementById('currencySymbolId').innerText = userCurrencyDetailsJSON["currencySymbol"];
    
    setFormData('addEditRecurringForm', record);
    
    // Open up the Bootstrap modal visually
    showHideAddEditRecurringModal(true);
}


// Retrives the data AFTER 1st ever request 
const doNavigation = function (event, navigationRequest, firstRequest) {

    if (event) {
        event.preventDefault();
    }

    recurringsPaginationJSON = firstRequest ? "" : getNewRecordStartAndEndNumber(recurringsPaginationJSON, navigationRequest);  // First request values will be set at post request regardless!!!

    const payload = {
        pagination: recurringsPaginationJSON,
        firstRequest: firstRequest
    };

    fetch('/transactions/recurring', {
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
                transactionRepeatIntervalListJSON = data.transactionRepeatIntervalList;
                recurringsPaginationJSON = data.pagination;
                userCurrencyDetailsJSON = data.userCurrencyDetails;
            }

            // Work with your parsed data object here
            displayRecurringData(data.recurrings, recurringsPaginationJSON);
        })
        .catch(error => {
            console.error("Fetch operation failed:", error);
        });

}

// Retrieve single record
const getRecurringRecord = function (recurringId) {

    fetch('/Recurring/GetRecurring', {
        method: 'POST',
        credentials: "include",   // REQUIRED for antiforgery cookie
        headers: {
            "Content-Type": "application/json",
            "RequestVerificationToken": _token
        },
        body: JSON.stringify({ recurringId: String(recurringId) })
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
            rebindValidation($("#addEditRecurringForm"));
            // Work with parsed data object here
            data.transactionRepeatInterval = data.transactionRepeatInterval || 3; // Default to "Monthly" in 'Edit' mode. * REQUIRED FIELD
            showAddEditRecurringModal(data);
        })
        .catch(error => {
            console.error("Fetch operation failed:", error);
        });
}

// Save Current Record or Add New Record!!
const saveRecurringRecord = function () {

    var data = getFormEntries('addEditRecurringForm');
    data.recurringAmount = Number(data.recurringAmount); // Convert String to a Number (null or '' will be converted to 0)
    data.transactionCategorization = data.transactionCategorization ? Number(data.transactionCategorization) : 1;
    data.isTransactionRepeatActive = Number(data.isTransactionRepeatActive);
    data.transactionRepeatId = Number(data.transactionRepeatId);    // blank will be converted to 0.

    data.transactionRepeatInterval = Number(data.transactionRepeatInterval);
    data.recurringNextDate = data.recurringNextDate || null;
    data.transactionRepeatEndDate = data.transactionRepeatEndDate || null;      // save the date value as is from the form's input type=date or null... correct way!!!
    
    fetch("/Recurring/SaveRecurring", {
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
                showHideAddEditRecurringModal(false);
                doNavigation(null, 'current');  // Refresh the page!
                showDbMessage("Recurring saved successfully.", true);
            }
            else {
                console.error(result.message);
                showDbMessage("Error: Recurring has not been saved successfully!!!", false);
                return;
            }

        });

}

// Deletes the entire series of recurring records if deleteSeries is true.
const deleteRecurringSeries = function (event) {

    if (confirm('Do you want to delete the recurring series?')) {

        event.preventDefault();
        const $form = $(event.target).closest("form");
        const recurringId = $form.find("[name='recurringId']").val();
        deleteRecurringRecord(recurringId, true);
        showHideAddEditRecurringModal(false);

    }

}

// Delete the Recurring Record!!!
const deleteRecurringRecord = function (recurringId, deleteSeries) {

    fetch('/Recurring/DeleteRecurring', {
        method: 'POST',
        credentials: "include",   // REQUIRED for antiforgery cookie
        headers: {
            "Content-Type": "application/json",
            "RequestVerificationToken": _token
        },
        body: JSON.stringify({ recurringId: String(recurringId), deleteSeries: deleteSeries })
    })
        .then(response => {
            // Check if the HTTP status code is 200-299 (Ok)
            if (!response.ok) {
                // throw new Error(`HTTP error! Status: ${response.status}`);
                showDbMessage(`Recurring ${deleteSeries ? 'series' : ''} has not been deleted successfully!!!`, true);
            }
            // Parse the body text as JSON
            return response.json();
        })
        .then(data => {
            doNavigation(null, 'current');  // Refresh the page!
            showDbMessage(`Recurring ${deleteSeries ? 'series' : ''} deleted successfully.`, true);
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
        recurringsDataMode = actionType;    // Edit and Delete will be confirmed here

        // Execute specific logic depending on what was clicked
        if (actionType === "Edit") {
            
            getRecurringRecord(recordId);

        } else if (actionType === "Delete") {
            if (confirm('Do you want to delete the recurring?')) {
                deleteRecurringRecord(recordId, false);
            }
        }

    }

});

// Add new recurring record
document.querySelector('span[title="Add"]').addEventListener('click', function (event) {
    recurringsDataMode = 'Add';
    clearFormData('#addEditRecurringForm');
    rebindValidation($("#addEditRecurringForm"));
    const record = getFormEntries('addEditRecurringForm');    // Retrieve a blank record!!!

    record.recurringNextDate = formatDateToYYYYMMDD(new Date());   // Today's Date In YYYY-MM-DD format. * REQUIRED FIELD
    record.transactionCategorization = 1; // Manual Entry
    record.transactionRepeatInterval = 3; // Default to "Monthly" in 'Add' mode. * REQUIRED FIELD
    
    showAddEditRecurringModal(record);
});

// Set Checkbox Value to 1 or 0 instead of true or false for the backend processing
document.getElementById("modalIsTransactionRepeatActive").addEventListener("change", e => {
    e.preventDefault();
    e.target.value = e.target.checked ? 1 : 0;
});


//  Set the Category Type value for the hidden field to validate amount!
document.getElementById('modalCategoryId').addEventListener("change", (e) => {

    e.preventDefault();
    document.getElementById('modalCategoryType').value = categoriesListJSON.find(c => c.listOptionId == e.target.value)?.listOptionType;

});

// Load the first time data!!!
doNavigation(null, 'first', true);

// Create Date Validator for UI Validation. 
// Date value must be greater than or equal to today's date.
$.validator.addMethod("dategte", function (value, element) {

    if (!value) return true; // Let 'required' validator handle empty inputs

    // Split 'YYYY-MM-DD' into numeric components
    const parts = value.split('-');

    // Construct date using local time: Date(year, monthIndex, day)
    const inputDate = new Date(parts[0], parts[1] - 1, parts[2]);

    const today = new Date();
    today.setHours(0, 0, 0, 0);

    return inputDate >= today;
});

// Add the adapter for unobtrusive validation
$.validator.unobtrusive.adapters.addBool("dategte");


// Validate Amount Value for Category!!
appendAmountValidationForCategoryType( 'categoryId' , 'recurringAmount' );

// Start Date must be lower than End Date
appendLessThanDateValueValidationFunctionality();

// Configure Unobtrusive Validations Settings On Modal and it's Form (MUST BE LAST ALWAYS)
// Attach the unobtrusive validation to the modal form after the DOM is ready
setUpUnobtrusiveValidationOnModal('#addEditRecurringModal');


// Save the recurring record
$(document).ready(function () {

    // Check validation on Save click
    $('#btnRecurringModalSaveChanges').on('click', function () {
        const $form = $('#addEditRecurringForm');
        if (!$form.valid()) {
            return;
        }
        console.log("Form is valid! Sending AJAX payload...");
        saveRecurringRecord();

    });

});

