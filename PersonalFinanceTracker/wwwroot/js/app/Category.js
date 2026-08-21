let _token = getSecurityToken('#addEditCategoryForm');
let prevQueriedRecord = '';
let categoriesPaginationJSON = '';
let standardCategoriesJSON = '';
let categoryTypesJSON = '';
let categoriesDataMode = '';


// Displays the data in the table in UI (<tbody id="categoriesTBody">)...
const displayCategoryData = function (categoriesData, pagination) {
    // Select the table body element
    const tbody = document.getElementById("categoriesTBody");

    // Clear existing contents safely
    tbody.innerHTML = "";

    // Iterate over the new data and append new rows
    categoriesData.forEach(item => {
        // Create a new row element
        const row = document.createElement("tr");
        //data-secure
        row.setAttribute("data-secure-index", item.categoryId);

        // Populate the row with specific cell data
        row.innerHTML = `<td><span class="color-label" style="background-color: ${item.categoryColor}">&nbsp;</span></td>
                                  <td>${item.categoryName}</td>
                                  <td>${categoryTypesJSON.find(t => t.categoryTypeId == item.categoryType)?.categoryTypeName}</td>
                                  <td>${item.categoryDescription}</td>
                                  <td>${item.categoryType == 0 ? '<span title="Locked" class="material-symbols-outlined-data-icon">lock</span>' : '<span title="Edit" class="material-symbols-outlined-data-icon">edit</span>'}</td>
                                  <td>${item.categoryType == 0 ? '' : '<span title="Delete" class="material-symbols-outlined-data-icon">delete</span>'}</td>`;

        // Append the complete row to the table body
        tbody.appendChild(row);
    });

    const row = getPaginationRow(pagination, 6);
    tbody.appendChild(row);
}

// Generates/Creates a new modal and shows or hide the same modal...
const showHideAddEditCategoryModal = function (show) {

    const addEditCategoryModalElement = document.getElementById('addEditCategoryModal');

    // Get the existing instance, or create a new one if it doesn't exist yet
    let modalInstance = bootstrap.Modal.getInstance(addEditCategoryModalElement);

    if (!modalInstance) {
        modalInstance = new bootstrap.Modal(addEditCategoryModalElement);
    }

    if (show) {

        if (categoriesDataMode == 'Add') {
            document.getElementById('addEditCategoryModalLabelId').innerHTML = '<span class="material-symbols-outlined-data-icon">add</span> Add Category';
        }
        if (categoriesDataMode == 'Edit') {
            document.getElementById('addEditCategoryModalLabelId').innerHTML = '<span class="material-symbols-outlined-data-icon">edit</span> Edit Category';
        }

        modalInstance.show();

    } else {

        modalInstance.hide();
    }

}

// Display the modal with Data
const showAddEditCategoryModal = function (record) {

    if (record) {
        populateSelectFromJSON('modalStandardizeCategory', standardCategoriesJSON, 'categoryId', 'categoryName', '-- Standard Category --');
        populateSelectFromJSON('modalCategoryType', categoryTypesJSON.filter(ct => ct.categoryTypeId != 0), 'categoryTypeId', 'categoryTypeName', '-- Category Type --');

        const categoryForm = document.getElementById('addEditCategoryForm');
        setFormData(categoryForm, record);
    }

    // Open up the Bootstrap modal visually
    showHideAddEditCategoryModal(true);
}


// Retrives the data AFTER 1st ever request 
const doNavigation = function (event, navigationRequest, firstRequest) {

    if (event) {
        event.preventDefault();
    }

    categoriesPaginationJSON = firstRequest ? "" : getNewRecordStartAndEndNumber(categoriesPaginationJSON, navigationRequest);  // First request values will be set at post request regardless!!!

    const payload = {
        pagination: categoriesPaginationJSON,
        firstRequest: firstRequest
    };

    fetch('/categories', {
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
                standardCategoriesJSON = data.standardCategories;
                categoryTypesJSON = data.categoryTypes
                categoriesPaginationJSON = data.pagination;     // Required only at first request
            }

            // Work with your parsed data object here
            displayCategoryData(data.categories, categoriesPaginationJSON);
        })
        .catch(error => {
            console.error("Fetch operation failed:", error);
        });

}

// Retrieve single record
const getCategoryRecord = function (categoryId) {

    fetch('/Category/GetCategory', {
        method: 'POST',
        credentials: "include",   // REQUIRED for antiforgery cookie
        headers: {
            "Content-Type": "application/json",
            "RequestVerificationToken": _token
        },
        body: JSON.stringify({ categoryId: String(categoryId) })
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
            showAddEditCategoryModal(data);
        })
        .catch(error => {
            console.error("Fetch operation failed:", error);
        });
}



// Updates the Standard Categories JSON at Client Side / Browser only for 'Edit' & 'Delete'
const updateStandardCategoriesJSON = function (record) {
    if (categoriesDataMode == 'Edit') {
        const standardCategoriesJSONItemIndex = standardCategoriesJSON.findIndex(t => t.categoryId === record.categoryId);
        if (standardCategoriesJSONItemIndex > 0) {
            standardCategoriesJSON[standardCategoriesJSONItemIndex].categoryName = record.categoryName;
        }
    }
    if (categoriesDataMode == 'Delete') {
        standardCategoriesJSON = standardCategoriesJSON.filter(t => t.categoryId !== record.categoryId);
    }
}

// Save Current Record or Add New Record!!
const saveCategoryRecord = function () {

    const form = document.getElementById('addEditCategoryForm');

    var data = getFormEntries(form);
    data.categoryDisplayOrder = +data.categoryDisplayOrder; // Convert String to a Number for small numbers
    data.categoryType = +data.categoryType;

    fetch("/Category/SaveCategory", {
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
                showHideAddEditCategoryModal(false);
                updateStandardCategoriesJSON(data);
                doNavigation(null, 'current');  // Refresh the page!
                showDbMessage("Category saved successfully.", true);
            }
            else {
                console.error(result.message);
                showDbMessage("Error: Category has not been saved successfully!!!", false);
                return;
            }

        });

}

// Delete the Category Record!!!
const deleteCategoryRecord = function (categoryId) {

    fetch('/Category/DeleteCategory', {
        method: 'POST',
        credentials: "include",   // REQUIRED for antiforgery cookie
        headers: {
            "Content-Type": "application/json",
            "RequestVerificationToken": _token
        },
        body: JSON.stringify({ categoryId: String(categoryId) })
    })
        .then(response => {
            // Check if the HTTP status code is 200-299 (Ok)
            if (!response.ok) {
                // throw new Error(`HTTP error! Status: ${response.status}`);
                showDbMessage("Category has not been deleted successfully!!!", true);
            }
            // Parse the body text as JSON
            return response.json();
        })
        .then(data => {
            updateStandardCategoriesJSON({ categoryId: categoryId });
            doNavigation(null, 'current');  // Refresh the page!
            showDbMessage("Category deleted successfully.", true);
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
        categoriesDataMode = actionType;    // Edit and Delete will be confirmed here

        // Execute specific logic depending on what was clicked
        if (actionType === "Edit") {

            if (recordId === prevQueriedRecord) {
                showAddEditCategoryModal(null); // Show the existing details
            }
            else {
                getCategoryRecord(recordId);
                prevQueriedRecord = recordId;
            }


        } else if (actionType === "Delete") {
            if (confirm('Do you want to delete the category?')) {
                deleteCategoryRecord(recordId);
            }
        }

    }

});

document.querySelector('span[title="Add"]').addEventListener('click', function (event) {
    prevQueriedRecord = '';
    categoriesDataMode = 'Add';
    clearFormData('#addEditCategoryForm');
    const categoryForm = document.getElementById('addEditCategoryForm');
    const record = getFormEntries(categoryForm);    // Retrieve a blank record!!!
    showAddEditCategoryModal(record);
});


// Load the first time data!!!
doNavigation(null, 'first', true);

// Configure Unobtrusive Validations Settings On Modal and it's Form
setUpUnobtrusiveValidationOnModal('#addEditCategoryModal', '#addEditCategoryForm');

$(document).ready(function () {

    // Check validation on Save click
    $('#btnCategoryModalSaveChanges').on('click', function () {
        const $form = $('#addEditCategoryForm');
        if (!$form.valid()) {
            return;
        }
        console.log("Form is valid! Sending AJAX payload...");
        saveCategoryRecord();

    });

});