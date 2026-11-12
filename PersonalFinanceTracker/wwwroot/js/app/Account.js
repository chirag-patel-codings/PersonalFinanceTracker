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
        // For Linked Accounts
        row.setAttribute("data-is-a-linked-account", item.isALinkedAccount);

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

// Changes the start and end record number for the request and Fetches the requested data from server and displays in UI...
// First request retrieves the common data like account types and pagination values from server, subsequent requests will not retrieve these common data from server.
// firstRequest is true for the first time request, false for subsequent requests. 
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
        const isALinkedAccount = row.getAttribute('data-is-a-linked-account');

        // Get the encrypted record and display or  process!!!
        const actionType = clickedAction.getAttribute('title');
        accountsDataMode = actionType;    // Edit and Delete will be confirmed here

        // Execute specific logic depending on what was clicked
        if (actionType === "Edit") {

            if (isALinkedAccount === "1") {
                // For Linked Accounts, we will not show the modal, instead we will open the Plaid Link to update the account details!
                setupPlaidLink("edit", recordId, null);
            }
            else {

                if (recordId === prevQueriedRecordId) {
                    showAddEditAccountModal(null); // Show the existing details
                }
                else {
                    getAccountRecord(recordId);
                    prevQueriedRecordId = recordId;
                }

            }

        } else if (actionType === "Delete") {

            if (isALinkedAccount === "1") {
                // For Linked Accounts, we will not show the modal, instead we will open the Plaid Link to update the account details
                if (confirm("To delete this account, you must unselect it in the next screen!\nPlease select another account if it is the only account linked and other accounts available for selection!\n\nYou may NOT be able to un-select, if it's the only account from an institution!\n\nDeleting this account, will delete all of it's related records from your account permanently!\n\nDo you want to proceed?")){
                    
                    setupPlaidLink("delete", recordId, () => {
                        deleteAccountRecord(recordId);
                    });
                }
            }
            else {
                if (confirm("Do you want to delete the account?\nDeleting this account, will delete all of it's related records from your account permanently!")) {
                    deleteAccountRecord(recordId);
                }
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
    record.isALinkedAccount = 0; // Account Created By User is not a linked account, so set it to 0
    showAddEditAccountModal(record);
});


// Plaid Create Token - Start

// Attach directly to your button click event
document.getElementById('addLinkedAccount').onclick = function () {
    
    setupPlaidLink("add", null, null);
    
};

// Define a global handler variable
let plaidHandler = null;

// Gets a temporary Token from Plaid which is required to open the Plaid Modal.
async function getPlaidTemporaryLinkToken(dataMode, accountId = null) {

    let requestUri = '/api/plaid/create-link-token';

    if(dataMode !== "add") {
        requestUri = '/api/plaid/create-update-link-token';
    }

    // Request a fresh link_token from your C# backend right when the user clicks
    const response = await fetch(requestUri, {
        method: 'POST',
        credentials: "include",   // REQUIRED for antiforgery cookie
        headers: {
            "Content-Type": "application/json",
            "RequestVerificationToken": _token
        },
        body: JSON.stringify({ accountId: accountId })
    });

    if (!response.ok) {
        throw new Error('Failed to fetch link token from backend');
    }

    const data = await response.json();
    return data.link_token;

}

// Links an external account through Plaid.
async function setupPlaidLink(dataMode, accountId = null, onCompleteCallback = null) {

    try {
        
        const linkToken = await getPlaidTemporaryLinkToken(dataMode, accountId);

        // Initialize Plaid Link Handler with the fresh token
        plaidHandler = Plaid.create({

            token: linkToken,
            // Only after the user successfully logs into their bank inside the pop-up does Plaid fire the onSuccess function
            onSuccess: async (public_token, metadata) => {

                try {
                    // Send public_token to your C# backend
                    const response = await fetch('/api/plaid/get-and-save-exchange-tokens', {
                        method: 'POST',
                        credentials: "include",
                        headers: {
                            "Content-Type": "application/json",
                            "RequestVerificationToken": _token
                        },
                        body: JSON.stringify({
                            plaidPublicTemporaryToken: public_token,
                            dataMode: dataMode // or "update" based on your logic
                        }),
                    });

                    if (!response.ok) {
                        throw new Error('Failed to exchange token on backend.');
                    }

                    const result = await response.json();
                    if (result.success) {

                        // EXECUTE CALLBACK HERE (After Plaid flow is fully done!)
                        if (typeof onCompleteCallback === 'function') {
                            await onCompleteCallback();
                        }
                        
                        showDbMessage("Linked account added successfully!", true);
                        
                        // Refresh UI or notify the user
                        // location.reload();
                        doNavigation(null, 'first', true);
                    }

                } catch (error) {
                    console.error('Error during token exchange:', error);
                }
            },
            onExit: (err, metadata) => {
                if (err) {
                    console.error('User left Plaid Link with error:', err);
                }
            },
        });

        // Open the modal immediately after creation
        plaidHandler.open();

    } catch (error) {
        console.error('Error initializing Plaid Link:', error);
    }
}

// Plaid Create Token - End


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

// Load the first time data!!!
window.addEventListener('load', (event) => {
    doNavigation(null, 'first', true);
});