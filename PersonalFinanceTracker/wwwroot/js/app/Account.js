let _token = getSecurityToken('#addEditAccountForm');
let accountsPaginationJSON = '';
let accountTypesJSON = '';
let accountsDataMode = '';
let prevQueriedRecordId = '';

// Displays the data in the table in UI (<tbody id="accountsTBody">)...
const displayAccountData = function (accountsData, pagination) {
    // Select the table body element
    const tbody = document.getElementById("accountsTBody");

    // Clear existing contents safely
    tbody.innerHTML = "";

    // Iterate over the new data and append new rows
    accountsData.forEach(item => {
        // Create a new row element
        const row = document.createElement("tr");
        //data-secure
        row.setAttribute("data-secure-index", item.accountId);

        // Populate the row with specific cell data
        row.innerHTML = `<td>${item.accountName}</td>
                                  <td>${item.accountInstitutionName}</td>
                                  <td>${accountTypesJSON.find(t => t.accountTypeId == item.accountTypeId)?.accountTypeName}</td>
                                  <td>${item.accountDescription}</td>
                                  <td><span title="Edit" class='material-symbols-outlined-data-icon'>edit</span></td>
                                  <td><span title="Delete" class='material-symbols-outlined-data-icon'>delete</span></td>`;

        // Append the complete row to the table body
        tbody.appendChild(row);
    });

    const row = getPaginationRow(pagination, 6);
    tbody.appendChild(row);
}

const doNavigation = function (event, navigationRequest, firstRequest) {

    if (event) {
        event.preventDefault();
    }

    accountsPaginationJSON = firstRequest ? "" : getNewRecordStartAndEndNumber(accountsPaginationJSON, navigationRequest); // First request values will be set at post request regardless!!!

    const payload = {
        pagination: accountsPaginationJSON,
        firstRequest: firstRequest
    };

    fetch('/accounts', {
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
                accountsPaginationJSON = data.pagination;
                accountTypesJSON = data.accountTypes;
            }
            // Work with your parsed data object here
            displayAccountData(data.accounts, accountsPaginationJSON);
        })
        .catch(error => {
            console.error("Fetch operation failed:", error);
        });

}

// Retrieve single record
const getAccountRecord = function (accountId) {

    fetch('/Account/GetAccount', {
        method: 'POST',
        credentials: "include",   // REQUIRED for antiforgery cookie
        headers: {
            "Content-Type": "application/json",
            "RequestVerificationToken": _token
        },
        body: JSON.stringify({ accountId: String(accountId) })
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
            rebindValidation($("#addEditAccountForm"));
            // Work with parsed data object here
            showAddEditAccountModal(data);
        })
        .catch(error => {
            console.error("Fetch operation failed:", error);
        });
}

// Generates/Creates a new modal and shows or hide the same modal...
const showHideAddEditAccountModal = function (show) {

    const addEditAccountModalElement = document.getElementById('addEditAccountModal');

    // Get the existing instance, or create a new one if it doesn't exist yet
    let modalInstance = bootstrap.Modal.getInstance(addEditAccountModalElement);

    if (!modalInstance) {
        modalInstance = new bootstrap.Modal(addEditAccountModalElement);
    }

    if (show) {

        if (accountsDataMode == 'Add') {
            document.getElementById('addEditAccountModalLabelId').innerHTML = "<span class='material-symbols-outlined-data-icon'>add</span> Add Account";
        }
        if (accountsDataMode == 'Edit') {
            document.getElementById('addEditAccountModalLabelId').innerHTML = "<span class='material-symbols-outlined-data-icon'>edit</span> Edit Account";
        }

        modalInstance.show();

    } else {

        modalInstance.hide();
    }

}

// Display the modal with Data
const showAddEditAccountModal = function (record) {
    if (record) {

        populateSelectFromJSON('modalAccountTypeId', accountTypesJSON, 'accountTypeId', 'accountTypeNameAndDescription', '-- Account Type--');

        setFormData('addEditAccountForm', record);

    }

    // Open up the Bootstrap modal visually
    showHideAddEditAccountModal(true);
}

// Save Current Record or Add New Record!!
const saveAccountRecord = function () {

    var data = getFormEntries('addEditAccountForm');

    data.accountTypeId = +data.accountTypeId;   // '+' urinary operator convers string to number if it's number
    data.isALinkedAccount = +data.isALinkedAccount;

    fetch("/Account/SaveAccount", {
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
                showHideAddEditAccountModal(false);
                doNavigation(null, 'current');  // Refresh the page!
                showDbMessage("Account saved successfully.", true);
            }
            else {
                console.error(result.message);
                showDbMessage("Error: Account has not been saved successfully!!!", false);
                return;
            }

        });

}

// Delete the Account Record!!!
const deleteAccountRecord = function (accountId) {

    fetch('/Account/DeleteAccount', {
        method: 'POST',
        credentials: "include",   // REQUIRED for antiforgery cookie
        headers: {
            "Content-Type": "application/json",
            "RequestVerificationToken": _token
        },
        body: JSON.stringify({ accountId: String(accountId) })
    })
        .then(response => {
            // Check if the HTTP status code is 200-299 (Ok)
            if (!response.ok) {
                // throw new Error(`HTTP error! Status: ${response.status}`);
                showDbMessage("Account has not been deleted successfully!!!", true);
            }
            // Parse the body text as JSON
            return response.json();
        })
        .then(data => {
            doNavigation(null, 'current');  // Refresh the page!
            showDbMessage("Account deleted successfully.", true);
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
        accountsDataMode = actionType;    // Edit and Delete will be confirmed here

        // Execute specific logic depending on what was clicked
        if (actionType === "Edit") {

            if (recordId === prevQueriedRecordId) {
                showAddEditAccountModal(null); // Show the existing details
            }
            else {
                getAccountRecord(recordId);
                prevQueriedRecordId = recordId;
            }

        } else if (actionType === "Delete") {
            if (confirm("Do you want to delete the account?\nDeleting this account, will delete all of it's related records from transactions!")) {
                deleteAccountRecord(recordId);
            }
        }

    }

});

document.querySelector('span[title="Add"]').addEventListener('click', function (event) {
    prevQueriedRecordId = '';
    accountsDataMode = 'Add';
    clearFormData('#addEditAccountForm');
    rebindValidation($("#addEditAccountForm"));
    const record = getFormEntries('addEditAccountForm');    // Retrieve a blank record!!!
    showAddEditAccountModal(record);
});


// Load the first time data!!!
doNavigation(null, 'first', true);

// Configure Unobtrusive Validations Settings On Modal and it's Form
setUpUnobtrusiveValidationOnModal('#addEditAccountModal');

$(document).ready(function () {

    // Check validation on Save click
    $('#btnAccountModalSaveChanges').on('click', function () {
        const $form = $('#addEditAccountForm');
        if (!$form.valid()) {
            return;
        }
        console.log("Form is valid! Sending AJAX payload...");
        saveAccountRecord();

    });

});