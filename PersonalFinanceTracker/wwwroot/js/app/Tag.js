let _token = getSecurityToken('#addEditTagForm');
let prevQueriedRecordId = '';
let tagsPaginationJSON = '';
let tagsDataMode = '';

// Displays the data in the table in UI (<tbody id="tagsTBody">)...
const displayTagData = function (tagsData, pagination) {
    // Select the table body element
    const tbody = document.getElementById("tagsTBody");

    // Clear existing contents safely
    tbody.innerHTML = "";

    // Iterate over the new data and append new rows
    tagsData.forEach(item => {
        // Create a new row element
        const row = document.createElement("tr");
        //data-secure
        row.setAttribute("data-secure-index", item.tagId);

        // Populate the row with specific cell data
        row.innerHTML = `<td><span class="color-label" style="background-color: ${item.tagColor}">&nbsp;</span></td>
                                  <td>${item.tagName}</td>
                                  <td>${item.tagDescription}</td>
                                  <td><span title="Edit" class="material-symbols-outlined-data-icon">edit</span></td>
                                  <td><span title="Delete" class="material-symbols-outlined-data-icon">delete</span></td>`;

        // Append the complete row to the table body
        tbody.appendChild(row);
    });

    const row = getPaginationRow(pagination, 5);
    tbody.appendChild(row);
}


const doNavigation = function (event, navigationRequest, firstRequest) {

    if (event) {
        event.preventDefault();
    }

    tagsPaginationJSON = firstRequest ? "" : getNewRecordStartAndEndNumber(tagsPaginationJSON, navigationRequest);  // First request values will be set at post request regardless!!!

    const payload = {
        pagination: tagsPaginationJSON,
        firstRequest: firstRequest
    };

    fetch('/categories/tags', {
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
                tagsPaginationJSON = data.pagination;
            }
            // Work with your parsed data object here
            displayTagData(data.tags, tagsPaginationJSON);
        })
        .catch(error => {
            console.error("Fetch operation failed:", error);
        });

}

// Retrieve single record
const getTagRecord = function (tagId) {

    fetch('/Tag/GetTag', {
        method: 'POST',
        credentials: "include",
        headers: {
            "Content-Type": "application/json",
            "RequestVerificationToken": _token
        },
        body: JSON.stringify({ "tagId": String(tagId) })
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
            rebindValidation($("#addEditTagForm"));
            // Work with parsed data object here
            showAddEditTagModal(data);
        })
        .catch(error => {
            console.error("Fetch operation failed:", error);
        });
}

// Generates/Creates a new modal and shows or hide the same modal...
const showHideAddEditTagModal = function (show) {

    const addEditTagModalElement = document.getElementById('addEditTagModal');

    // Get the existing instance, or create a new one if it doesn't exist yet
    let modalInstance = bootstrap.Modal.getInstance(addEditTagModalElement);

    if (!modalInstance) {
        modalInstance = new bootstrap.Modal(addEditTagModalElement);
    }

    if (show) {

        if (tagsDataMode == 'Add') {
            document.getElementById('addEditTagModalLabelId').innerHTML = "<span class='material-symbols-outlined-data-icon'>add</span> Add Tag";
        }
        if (tagsDataMode == 'Edit') {
            document.getElementById('addEditTagModalLabelId').innerHTML = "<span class='material-symbols-outlined-data-icon'>edit</span> Edit Tag";
        }

        modalInstance.show();

    } else {

        modalInstance.hide();
    }

}

// Display the modal with Data
const showAddEditTagModal = function (record) {

    if (record) {
        setFormData('addEditTagForm', record);
    }
    // Open up the Bootstrap modal visually
    showHideAddEditTagModal(true);
}

// Save Current Record or Add New Record!!
const saveTagRecord = function () {

    var data = getFormEntries('addEditTagForm');

    fetch("/Tag/SaveTag", {
        method: "POST",
        credentials: "include",
        body: JSON.stringify(data),
        headers: {
            "Content-Type": "application/json",
            "RequestVerificationToken": _token
        }
    })
        .then(res => res.json())
        .then(result => {
            if (result.success) {
                showHideAddEditTagModal(false);
                doNavigation(null, 'current');  // Refresh the page!
                showDbMessage("Tag saved successfully.", true);
            }
            else {
                console.error(result.message);
                showDbMessage("Error: Tag has not been saved successfully!!!", false);
                return;
            }
        });

}

// Delete the Tag Record!!!
const deleteTagRecord = function (tagId) {

    fetch('/Tag/DeleteTag', {
        method: 'POST',
        credentials: "include",
        headers: {
            "Content-Type": "application/json",
            "RequestVerificationToken": _token
        },
        body: JSON.stringify({ "tagId": String(tagId) })
    })
        .then(response => {
            // Check if the HTTP status code is 200-299 (Ok)
            if (!response.ok) {
                // throw new Error(`HTTP error! Status: ${response.status}`);
                showDbMessage("Tag has not been deleted successfully!!!", true);
            }
            // Parse the body text as JSON
            return response.json();
        })
        .then(data => {
            doNavigation(null, 'current');  // Refresh the page!
            showDbMessage("Tag deleted successfully.", true);
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
        tagsDataMode = actionType;    // Edit and Delete will be confirmed here

        // Execute specific logic depending on what was clicked
        if (actionType === "Edit") {

            if (recordId === prevQueriedRecordId) {
                showAddEditTagModal(null); // Show the existing details
            }
            else {

                getTagRecord(recordId);
                prevQueriedRecordId = recordId;
            }

        } else if (actionType === "Delete") {
            if (confirm('Do you want to delete the tag?')) {
                deleteTagRecord(recordId);
            }
        }

    }

});

document.querySelector('span[title="Add"]').addEventListener('click', function (event) {
    prevQueriedRecordId = '';
    tagsDataMode = 'Add';
    clearFormData('#addEditTagForm');
    rebindValidation($("#addEditTagForm"));
    const record = getFormEntries('addEditTagForm');    // Retrieve a blank record!!!
    showAddEditTagModal(record);
});

// Load the first time data!!!
doNavigation(null, 'first', true);

// Configure Unobtrusive Validations Settings On Modal and it's Form
setUpUnobtrusiveValidationOnModal('#addEditTagModal');

$(document).ready(function () {

    // Check validation on Save click
    $('#btnTagModalSaveChanges').on('click', function () {
        const $form = $('#addEditTagForm');
        if (!$form.valid()) {
            return;
        }
        console.log("Form is valid! Sending AJAX payload...");
        saveTagRecord();

    });

});