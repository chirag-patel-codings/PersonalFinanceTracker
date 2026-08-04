// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

// Configuration defaults


// Core function to completely purge and reinstall validation states
function rebindValidation($form) {
    // Completely remove data and delete the validator instance cache
    $form.removeData("validator");
    $form.removeData("unobtrusiveValidation");

    // Clear out jQuery's internal global validation cache completely
    if ($.validator) {
        $.validator.unobtrusive.parse($form);
    }

    const validator = $form.data('validator');
    if (validator) {
        // Disable native aggressive focusout validation
        validator.settings.onfocusout = false;

        // Clean previous validation classes off inputs so they don't look red on fresh load
        $form.find('.input-validation-error')
            .removeClass('input-validation-error')
            .addClass('valid');

        // Bind crisp, clean blur tracking to inputs
        $form.off('blur', 'input').on('blur', 'input', function () {
            validator.element(this);
        });

        $form.off('blur', 'select').on('blur', 'select', function () {
            validator.element(this);
        });
    }
}

// Configures Unobtrusive Validations Settings like clearing out error messages on modal hidden On 'Modal' with form!!!
// Also clears the values of the form controls on modal hidden
const setUpUnobtrusiveValidationOnModal = function (modalId, formId) {

    $(document).ready(function () {

        // Fires when the edit modal completely pops up
        $(modalId).on('shown.bs.modal', function () {
            const $form = $(formId);
            rebindValidation($form);
        });

        // Clears errors out completely when the modal window closes
        $(modalId).on('hidden.bs.modal', function () {
            const $form = $(formId);

            // My Code: Clear the existing data on modal form!!!
            $form[0].reset();
            $form.find('input[type="hidden"]').val('');
            
            // Reset selects (this is the missing piece)
            $form.find('select').each(function () {
                $(this).val('');          // clear value
                $(this).trigger('change'); // update UI (important for Bootstrap)
            });
            
            if ($form.data('validator')) {
                $form.validate().resetForm();
            }

            $form.removeData("validator");
            $form.removeData("unobtrusiveValidation");

            // Clean error styles from inputs and text messages
            $form.find('input, select, textarea').removeClass('input-validation-error');
            $form.find('[data-valmsg-for]')
                .text('')
                .removeClass('field-validation-error')
                .addClass('field-validation-valid');

        });

        // Any element on the modal has focus, will be blurred
        $(modalId).on('hide.bs.modal', function () {
            if (this.contains(document.activeElement)) {
                document.activeElement.blur();
            }
        });
        
    });
}


// Prepares and returns the row element with pagination controls enabled/disabled based upon the values of the paginationJSON object!!!
const getPaginationRow = function (paginationJSON, noOfColumnsInTable) {

    const row = document.createElement("tr");
    row.innerHTML = `<td colspan="${noOfColumnsInTable}">
                        <ul class="pagination mb-0 d-flex align-items-center justify-content-end w-100">
                            <li class='page-item ${paginationJSON.recordStartNumber == 1 ? "disabled" : ""}'><a class="page-link" href="#" title="First" onclick="doNavigation(event, 'first');">|<</a></li>
                            <li class='page-item ${paginationJSON.recordStartNumber - paginationJSON.pageSize < 1 ? "disabled" : ""}'><a class="page-link" href="#" title="Previous" onclick="doNavigation(event, 'prev');"><</a></li>
                            <li class="page-item ${paginationJSON.recordStartNumber + paginationJSON.pageSize > paginationJSON.totalNumberOfRecords ? "disabled" : ""}"><a class="page-link" href="#" title="Next" onclick="doNavigation(event,'next' );">></a></li>
                            <li class="page-item ${paginationJSON.recordStartNumber + paginationJSON.pageSize > paginationJSON.totalNumberOfRecords ? "disabled" : ""}"><a class="page-link" href="#" title="Last" onclick="doNavigation(event, 'last');">>|</a></li>
                        </ul>
                    </td>`;
    return row;
}


const getNewRecordStartAndEndNumber = function (paginationJSON, navigationRequest) {

    let recStartNumber = Number(paginationJSON.recordStartNumber);
    let recEndNumber = Number(paginationJSON.recordEndNumber);
    const pageSize = Number(paginationJSON.pageSize);
    const totalRecords = Number(paginationJSON.totalNumberOfRecords);

    switch (navigationRequest) {
        case 'first':
            recStartNumber = 1;
            recEndNumber = pageSize;
            break;
        case 'prev':
            recStartNumber = recStartNumber - pageSize;
            recEndNumber = recEndNumber - pageSize;
            break;
        case 'next':
            if (recEndNumber < totalRecords) {
                recStartNumber = recStartNumber + pageSize;
                recEndNumber = recStartNumber + pageSize - 1;
            }
            break;
        case 'last':
            recStartNumber = (Math.floor(totalRecords / pageSize) * pageSize) + 1;
            recEndNumber = recStartNumber + pageSize - 1;
            break;
        default:
            recStartNumber = recStartNumber;
            recEndNumber = recEndNumber;
            break;
    }

    return {
            pageSize: pageSize,
            recordStartNumber: recStartNumber,
            recordEndNumber: recEndNumber,
            totalNumberOfRecords: totalRecords
           };
}


// Gets returns the antiforgery token and they removes the generated hidden element from form
const getSecurityToken = function (formElementId) {
    const token = document.querySelector(formElementId + ' input[name="__RequestVerificationToken"]').value;
    document.querySelector(formElementId + ' input[name="__RequestVerificationToken"]').remove();
    return token;
}

// Checks if it's a valid date
function isValidDate(value) {
    const d = new Date(value);

    return (
        value !== "" &&                // not empty
        !isNaN(d.getTime()) &&        // Date object is valid
        value === d.toISOString().split("T")[0] // matches YYYY-MM-DD format
    );
}

// Creates a Disabled Blank Option
const createBlankOptionForSelect = function (blankOptionText) {

    const blankOption = document.createElement("option");
    blankOption.value = "";
    blankOption.textContent = blankOptionText;
    blankOption.disabled = true;
    blankOption.selected = true;
    return blankOption;

}

// Populates the 'select' on the form with the supplied JSON Array of data.
// selectElementName must be element Name NOT Id and It's Name and Case must match the Model's member names.
const populateSelectFromJSON = function (selectElementName, jsonArray, valueField, textField, blankOptionText) {

    var selectElement = document.getElementById(selectElementName);
    // Clear existing options
    selectElement.innerHTML = "";

    // Add blank first option
    if (blankOptionText) {
        selectElement.append(createBlankOptionForSelect(blankOptionText));
    }

    jsonArray.forEach(item => {
        const option = document.createElement("option");
        option.value = item[valueField];        // CategoryTypeId
        option.textContent = item[textField];   // CategoryTypeName
        selectElement.appendChild(option);
    });
}

// Function to strip "-index" from all key names
const changeKeyIndexes = function (obj) {
    return Object.entries(obj).reduce((acc, [key, value]) => {
        // Replace "-<number>" at the end of the key string
        const cleanKey = key.replace(/-\d+$/, '');

        acc[cleanKey] = value;
        return acc;
    }, {});
}

// Element Name required with the same as Model Record Name & Case
// Returns JSON 
const getFormEntries = function (form) {
    const formData = new FormData(form);

    // Convert all named inputs instantly into a structured JavaScript object
    const formResults = Object.fromEntries(formData);
    
    // Fix: ensure selects with blank disabled options are included
    form.querySelectorAll('select').forEach(select => {
        const name = select.name;
        const value = select.value;

        // If FormData skipped it, add it manually
        if (!formResults.hasOwnProperty(name)) {
            formResults[name] = value === "" ? "" : value;
        }
    });

    return formResults;
}


// This function will work if all the elements in the form has name and it is the same as Record Model Element Name & in Same Case
const setFormData = function (form, record){
    // Iterate through each form element
    for (const element of form.elements) {
        // Check if the element has a name attribute (filters out generic buttons)
        if (element.name) {
            element.value = record[element.name];
        }
    }
}


// Displays the message in the 'dbMessage' div, TO BE USED FOR DATABASE OPERATION RESULT!!!
function showDbMessage(message, isSuccess = true) {
    const msgDiv = document.getElementById("dbMessage");

    // Set message text
    msgDiv.textContent = message;

    // Apply Bootstrap classes
    msgDiv.className = "alert " + (isSuccess ? "alert-success" : "alert-danger");

    // Show the alert
    msgDiv.style.display = "block";

    // Fade in
    setTimeout(() => msgDiv.classList.add("show"), 10);

    // Fade out after 1 second
    setTimeout(() => {
        msgDiv.classList.remove("show");
        setTimeout(() => msgDiv.style.display = "none", 400);
    }, 1000);
}