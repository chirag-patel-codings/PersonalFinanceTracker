let _token = getSecurityToken('#addEditGoalFormId');
let prevQueriedRecord = '';
let goalsPaginationJSON = '';
let goalTypesJSON = '';
let goalIntervalTypesJSON = '';


// Add the goal detail form to the 'modalGoalDetailsContainerId' div element.
const appendGoalDetailForm = function (record) {

    const form = generateGoalDetailsForm(record);
    document.getElementById('modalGoalDetailsContainerId').appendChild(form);

    // Parse validation directly without waiting for modal events FOR THIS FORM!
    rebindValidation($(form));
    // $(form).validate();      // Does NOT parse dynamic fields

    // Force unobtrusive parser to read all data-val-* attributes FOR THIS FORM!
    $.validator.unobtrusive.parse($(form));

}

// Generates the forms for all the budget settings records. Single form for each record!!!
const generateGoalDetailsForm = function (record) {

    // Create the main form element
    const form = document.createElement('form');
    const elementsIdIndex = document.querySelectorAll('#modalGoalDetailsContainerId form').length + 1
    form.id = 'formGoalDetailId-' + elementsIdIndex;
    form.name = form.id.replace("Id", "Name");
    form.className = 'goal-details-form';
    form.setAttribute('data-val', 'true');
    form.style = "border-bottom: 1px solid #e0e0e0;";

    // All the elements container
    const divContainer = document.createElement('div');
    divContainer.className = 'd-flex align-items-center w-100';

    // Create the input controls container row container
    const rowDiv = document.createElement('div');
    rowDiv.className = 'row d-flex flex-wrap align-items-center p-1';
    rowDiv.style.width = '95%';
    // --- COLUMN 1: Goal Period Start Date ---
    const col1 = document.createElement('div');
    col1.className = 'col-sm field pe-2 d-flex flex-column';

    const goalPeriodStartDate = document.createElement('input');
    goalPeriodStartDate.type = 'date';
    goalPeriodStartDate.id = 'modalGoalPeriodStartDateId-' + elementsIdIndex;
    goalPeriodStartDate.name = 'goalPeriodStartDate-' + elementsIdIndex;
    goalPeriodStartDate.className = 'form-control';
    goalPeriodStartDate.style.width = '225px';
    goalPeriodStartDate.style.height = '40px';
    goalPeriodStartDate.setAttribute('placeholder', ' ');
    goalPeriodStartDate.setAttribute("data-val", "true");
    goalPeriodStartDate.setAttribute("data-val-required", "Please provide the goal start date!");
    goalPeriodStartDate.setAttribute("data-val-lessthan", "Start date must be earlier than end date!");
    goalPeriodStartDate.setAttribute("data-val-lessthan-other", "goalPeriodEndDate-" + elementsIdIndex);

    goalPeriodStartDate.value = record ? record.goalPeriodStartDate : ""; // Populate dynamically

    // Label
    const goalPeriodStartDateLabel = document.createElement('label');
    goalPeriodStartDateLabel.className = 'form-check-label input-label';
    goalPeriodStartDateLabel.htmlFor = 'modalGoalPeriodStartDateId-' + elementsIdIndex;
    goalPeriodStartDateLabel.textContent = '* Goal Period Start Date';

    // Error Message
    const goalPeriodStartDateSpan = document.createElement('span');
    goalPeriodStartDateSpan.setAttribute("data-valmsg-for", "goalPeriodStartDate-" + elementsIdIndex);
    goalPeriodStartDateSpan.setAttribute("data-valmsg-replace", "true");
    goalPeriodStartDateSpan.className = "text-danger field-validation-valid";

    col1.appendChild(goalPeriodStartDate);
    col1.appendChild(goalPeriodStartDateLabel);
    col1.appendChild(goalPeriodStartDateSpan);
    rowDiv.appendChild(col1);

    // --- COLUMN 2: Goal Period End Date ---
    const col2 = document.createElement('div');
    col2.className = 'col-sm field pe-2 d-flex flex-column';

    const goalPeriodEndDate = document.createElement('input');
    goalPeriodEndDate.type = 'date';
    goalPeriodEndDate.id = 'modalGoalPeriodEndDateId-' + elementsIdIndex;
    goalPeriodEndDate.name = 'goalPeriodEndDate-' + elementsIdIndex;
    goalPeriodEndDate.className = 'form-control';
    goalPeriodEndDate.style.width = '225px';
    goalPeriodEndDate.style.height = '40px';
    goalPeriodEndDate.setAttribute('placeholder', ' ');
    goalPeriodEndDate.setAttribute("data-val", "true");
    goalPeriodEndDate.setAttribute("data-val-required", "Please provide the goal end date!");
    goalPeriodEndDate.value = record ? record.goalPeriodEndDate : ""; // Populate dynamically

    // Label
    const goalPeriodEndDateLabel = document.createElement('label');
    goalPeriodEndDateLabel.className = 'form-check-label input-label';
    goalPeriodEndDateLabel.htmlFor = 'modalGoalPeriodEndDateId-' + elementsIdIndex;
    goalPeriodEndDateLabel.textContent = '* Goal Period End Date';

    // Error Message
    const goalPeriodEndDateSpan = document.createElement('span');
    goalPeriodEndDateSpan.setAttribute("data-valmsg-for", "goalPeriodEndDate-" + elementsIdIndex);
    goalPeriodEndDateSpan.setAttribute("data-valmsg-replace", "true");
    goalPeriodEndDateSpan.className = "text-danger field-validation-valid";

    col2.appendChild(goalPeriodEndDate);
    col2.appendChild(goalPeriodEndDateLabel);
    col2.appendChild(goalPeriodEndDateSpan);
    rowDiv.appendChild(col2);

    // --- COLUMN 3: Goal Interval Dropdown ---
    const col3 = document.createElement('div');
    col3.className = 'col-sm field pe-2 d-flex flex-column';

    const selectGoalPeriodInterval = document.createElement('select');
    selectGoalPeriodInterval.id = 'modalGoalIntervalId-' + elementsIdIndex;
    selectGoalPeriodInterval.name = 'goalInterval-' + elementsIdIndex;
    selectGoalPeriodInterval.className = 'form-control';
    selectGoalPeriodInterval.style.width = '225px';
    selectGoalPeriodInterval.style.height = '40px';
    selectGoalPeriodInterval.setAttribute('placeholder', ' ');
    selectGoalPeriodInterval.setAttribute("data-val", "true");
    selectGoalPeriodInterval.setAttribute("data-val-required", "Please provide the goal interval!");

    // Add dropdown options and set active selection conditionally
    selectGoalPeriodInterval.add(createBlankOptionForSelect(' -- Select Goal Interval -- ', true));
    let goalIntervalTypeOption = null;
    goalIntervalTypesJSON.forEach((item) => {
        goalIntervalTypeOption = new Option(item.listOptionName, item.listOptionId);
        selectGoalPeriodInterval.add(goalIntervalTypeOption);
    });
    selectGoalPeriodInterval.style.padding = "8px 12px";
    selectGoalPeriodInterval.value = record ? record.goalInterval : "";

    const labelGoalPeriodInterval = document.createElement('label');
    labelGoalPeriodInterval.htmlFor = 'modalGoalIntervalId-' + elementsIdIndex;
    labelGoalPeriodInterval.className = 'input-label';
    labelGoalPeriodInterval.textContent = '* Goal Interval';

    // Error Message
    const spanGoalPeriodInterval = document.createElement('span');
    spanGoalPeriodInterval.setAttribute("data-valmsg-for", "goalInterval-" + elementsIdIndex);
    spanGoalPeriodInterval.setAttribute("data-valmsg-replace", "true");
    spanGoalPeriodInterval.className = "text-danger field-validation-valid";

    col3.appendChild(selectGoalPeriodInterval);
    col3.appendChild(labelGoalPeriodInterval);
    col3.appendChild(spanGoalPeriodInterval);
    rowDiv.appendChild(col3);

    // --- COLUMN 4: Goal Amount ---
    const col4 = document.createElement('div');
    col4.className = 'col-sm field pe-2 d-flex flex-column';

    const goalPeriodAmount = document.createElement('input');
    goalPeriodAmount.type = 'number';
    goalPeriodAmount.step = '0.01';
    goalPeriodAmount.id = 'modalGoalAmountId-' + elementsIdIndex;
    goalPeriodAmount.name = 'goalAmount-' + elementsIdIndex;
    goalPeriodAmount.className = 'form-control';
    goalPeriodAmount.style.width = '225px';
    goalPeriodAmount.style.height = '40px';
    goalPeriodAmount.style.alignContent = 'right';
    goalPeriodAmount.setAttribute('placeholder', ' ');
    goalPeriodAmount.setAttribute("data-val", "true");
    goalPeriodAmount.setAttribute("data-val-required", "Please provide the goal interval!");
    goalPeriodAmount.value = record ? record.goalAmount : ""; // Populate dynamically

    // Label
    const goalPeriodAmountLabel = document.createElement('label');
    goalPeriodAmountLabel.className = 'form-check-label input-label';
    goalPeriodAmountLabel.htmlFor = 'modalGoalAmountId-' + elementsIdIndex;
    goalPeriodAmountLabel.textContent = '* Goal Amount';


    // Error Message
    const goalPeriodAmountSpan = document.createElement('span');
    goalPeriodAmountSpan.setAttribute("data-valmsg-for", "goalAmount-" + elementsIdIndex);
    goalPeriodAmountSpan.setAttribute("data-valmsg-replace", "true");
    goalPeriodAmountSpan.className = "text-danger field-validation-valid";

    col4.appendChild(goalPeriodAmount);
    col4.appendChild(goalPeriodAmountLabel);
    col4.appendChild(goalPeriodAmountSpan);

    rowDiv.appendChild(col4);

    divContainer.appendChild(rowDiv);

    // --- COLUMN 5: Form Removal ---
    const col5 = document.createElement('div');
    col5.style.width = '5%';
    col5.className = 'd-flex justify-content-end align-items-center';
    col5.innerHTML = '<h3 class="m-0"><span class="material-symbols-outlined-data-icon">remove</span></h3>';
    col5.title = "Delete detail";

    col5.addEventListener("click", (e) => {
        e.target.closest("form").remove();
    });

    divContainer.appendChild(col5);

    form.appendChild(divContainer);

    return form;

}

// Displays the data in the table in UI (<tbody id="goalsTBody">)...
const displayGoalData = function (goalsData, pagination) {
    // Select the table body element
    const tbody = document.getElementById("goalsTBody");

    // Clear existing contents safely
    tbody.innerHTML = "";

    // Iterate over the new data and append new rows
    goalsData.forEach(item => {
        // Create a new row element
        const row = document.createElement("tr");
        //data-secure
        row.setAttribute("data-secure-index", item.goalId);

        // Populate the row with specific cell data
        row.innerHTML = `<td>${item.goalName}</td>
                                  <td>${item.goalDescription}</td>
                                  <td>${goalTypesJSON.find(t => t.listOptionId == item.goalType)?.listOptionName}</td>
                                  <td><span title="Edit" class="material-symbols-outlined-data-icon">edit</span></td>
                                  <td><span title="Delete" class="material-symbols-outlined-data-icon">delete</span></td>`;

        // Append the complete row to the table body
        tbody.appendChild(row);
    });

    const row = getPaginationRow(pagination, 5);
    tbody.appendChild(row);
}

// Generates/Creates a new modal and shows or hide the same modal...
const showHideAddEditGoalModal = function (show) {

    const addEditGoalModalElement = document.getElementById('addEditGoalModal');

    // Get the existing instance, or create a new one if it doesn't exist yet
    let modalInstance = bootstrap.Modal.getInstance(addEditGoalModalElement);

    if (!modalInstance) {
        modalInstance = new bootstrap.Modal(addEditGoalModalElement);
    }

    if (show) {

        if (goalsDataMode == 'Add') {
            document.getElementById('addEditGoalModalLabelId').innerHTML = "<span class='material-symbols-outlined-data-icon'>add</span> Add Goal";
        }
        if (goalsDataMode == 'Edit') {
            document.getElementById('addEditGoalModalLabelId').innerHTML = "<span class='material-symbols-outlined-data-icon'>edit</span> Edit Goal";
        }

        modalInstance.show();

    } else {

        modalInstance.hide();
    }

}

// Display the modal with Data
const showAddEditGoalModal = function (data) {

    if (data) {
        document.getElementById('modalGoalDetailsContainerId').innerHTML = '';  // remove all the exsiting DETAILS
        populateSelectFromJSON('modalGoalTypeId', goalTypesJSON, 'listOptionId', 'listOptionName', '-- Goal Type --');

        const goalForm = document.getElementById('addEditGoalFormId');
        setFormData(goalForm, data);

        data.goalDetails?.forEach((record) => {
            appendGoalDetailForm(record);
        });
    }

    // Open up the Bootstrap modal visually
    showHideAddEditGoalModal(true);
}

// Retrives the data
const doNavigation = function (event, navigationRequest, firstRequest) {

    if (event) {
        event.preventDefault();
    }

    goalsPaginationJSON = firstRequest ? "" : getNewRecordStartAndEndNumber(goalsPaginationJSON, navigationRequest);  // First request values will be set at post request regardless!!!

    const payload = {
        pagination: goalsPaginationJSON,
        firstRequest: firstRequest
    };

    fetch('/goals', {
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
                goalTypesJSON = data.goalTypes;
                goalIntervalTypesJSON = data.goalIntervalTypes;
                goalsPaginationJSON = data.pagination;     // Required only at first request
            }

            // Work with your parsed data object here
            displayGoalData(data.goals, goalsPaginationJSON);
        })
        .catch(error => {
            console.error("Fetch operation failed:", error);
        });

}

// Retrieve single Goal record
const getGoalRecord = function (goalId) {

    fetch('/Goal/GetGoal', {
        method: 'POST',
        credentials: "include",   // REQUIRED for antiforgery cookie
        headers: {
            "Content-Type": "application/json",
            "RequestVerificationToken": _token
        },
        body: JSON.stringify({ goalId: String(goalId) })
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
            // Work with parsed data object here
            showAddEditGoalModal(data);
        })
        .catch(error => {
            console.error("Fetch operation failed:", error);
        });
}

// Data to be retrieved from every main and child forms (BEFORE SAVE)!!!
const getGoalAndDetailsData = function () {

    const form = document.getElementById('addEditGoalFormId');
    var data = getFormEntries(form);
    data.goalType = +data.goalType;     // Convert String to a Number for small numbers

    data.goalDetails = [];
    document.querySelectorAll('#modalGoalDetailsContainerId form').forEach((detailsForm) => {
        var detail = getFormEntries(detailsForm);
        detail = changeKeyIndexes(detail);      // remove the trailing number and '-' from field name
        detail.goalInterval = Number(detail.goalInterval);
        detail.goalAmount = Number(detail.goalAmount);
        data.goalDetails.push(detail);
    });
    return data;
}

// Save Current Record or Add New Record with details!!
const saveGoalRecord = function () {

    var data = getGoalAndDetailsData();

    fetch("/Goal/SaveGoal", {
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
                showHideAddEditGoalModal(false);
                doNavigation(null, 'current');  // Refresh the page!
                showDbMessage("Goal saved successfully.", true);
            }
            else {
                console.error(result.message);
                showDbMessage("Error: Goal has not been saved successfully!!!", false);
                return;
            }
        });
}

// Delete the Goal Record!!!
const deleteGoalRecord = function (goalId) {

    fetch('/Goal/DeleteGoal', {
        method: 'POST',
        credentials: "include",   // REQUIRED for antiforgery cookie
        headers: {
            "Content-Type": "application/json",
            "RequestVerificationToken": _token
        },
        body: JSON.stringify({ goalId: String(goalId) })
    })
        .then(response => {
            // Check if the HTTP status code is 200-299 (Ok)
            if (!response.ok) {
                // throw new Error(`HTTP error! Status: ${response.status}`);
                showDbMessage("Goal has not been deleted successfully!!!", true);
            }
            // Parse the body text as JSON
            return response.json();
        })
        .then(data => {
            doNavigation(null, 'current');  // Refresh the page!
            showDbMessage("Goal deleted successfully.", true);
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
        goalsDataMode = actionType;    // Edit and Delete will be confirmed here

        // Execute specific logic depending on what was clicked
        if (actionType === "Edit") {

            if (recordId === prevQueriedRecord) {
                showAddEditGoalModal(null); // Show the existing details
            }
            else {
                getGoalRecord(recordId);
                prevQueriedRecord = recordId;
            }


        } else if (actionType === "Delete") {

            if (confirm('Do you want to delete the goal?')) {
                deleteGoalRecord(recordId);
            }

        }

    }

});

// ADD 
document.querySelector('span[title="Add"]').addEventListener('click', function (event) {
    prevQueriedRecord = '';
    goalsDataMode = 'Add';
    clearFormData('#addEditGoalFormId');
    const goalForm = document.getElementById('addEditGoalFormId');
    const record = getFormEntries(goalForm);    // Retrieve a blank record!!!
    showAddEditGoalModal(record);

});

// Load the first time data!!!
doNavigation(null, 'first', true);

// Start Date and End Date Validations...
appendLessThanDateValueValidationFunctionality();

// Configure Unobtrusive Validations Settings On Modal and it's MAIN Form
setUpUnobtrusiveValidationOnModal('#addEditGoalModal', '#addEditGoalFormId');

$('#btnGoalModalSaveChanges').on('click', function () {
    // Select the main form AND all detail/child forms
    // (Give your child forms a shared class, like .detail-form)
    const $mainForm = $('#addEditGoalFormId');
    const $detailForms = $('.goal-details-form');

    let allFormsValid = true;

    // Validate the main form first
    if (!$mainForm.valid()) {
        allFormsValid = false;
    }

    // Loop through every detail form and trigger validation
    $detailForms.each(function () {
        // Calling .valid() triggers visual error messages on this specific form
        if (!$(this).valid()) {
            allFormsValid = false;
        }
    });

    // Stop execution if ANY form failed validation
    if (!allFormsValid) {
        console.log("Validation failed on one or more forms.");
        return;
    }

    // Proceed with your save action if everything passed
    console.log("All forms are valid! Sending AJAX payload...");
    saveGoalRecord();
});