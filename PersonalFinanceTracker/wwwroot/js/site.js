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


// This functionality would work for any modules where appeneded and validates if the start date is less than end date...

const appendLessThanDateValueValidationFunctionality = function () {

    // Register globally ONCE
    $.validator.addMethod("lessThan", function (value, element, params) {
        if (!value) return true;

        var $form = $(element).closest("form");

        // Search by name first, then fallback to ID
        var $otherElement = $form.find("[name='" + params + "']");
        if (!$otherElement.length) {
            $otherElement = $form.find("#" + params);
        }

        var targetValue = $otherElement.val();
        if (!targetValue) return true;

        return new Date(value) <= new Date(targetValue);
    }, "Start date must be less than or equal to end date.");


    // ADD TO UNOBSTRUSIVE VALIDATOR
    $.validator.unobtrusive.adapters.add("lessThan", ["other"], function (options) {
        options.rules["lessThan"] = options.params.other;
        options.messages["lessThan"] = options.message;
    });


    // Re-validate both dates whenever user changes either field
    $(document).on("change blur", "input[type='date']", function () {
        const $this = $(this);
        const $form = $this.closest("form");

        // If the input changed IS the Start Date
        const targetName = $this.attr("data-val-lessthan-other");
        if (targetName) {
            $this.valid(); // Always validate Start Date on change/blur

            const $endDate = $form.find(`[name='${targetName}'], #${targetName}`);
            if ($endDate.length && $endDate.data("focused")) {
                // Trigger validation on End Date so required/date check fires if empty, or comparison fires if set
                $endDate.valid();
            }
            return;
        }

        // If the input changed IS the End Date
        const thisNameOrId = $this.attr("name") || $this.attr("id");
        if (thisNameOrId) {
            $this.valid(); // Always validate End Date on change/blur

            // Find the Start Date that points to this End Date
            const $startDate = $form.find(`[data-val-lessthan-other='${thisNameOrId}']`);
            if ($startDate.length && $startDate.data("focused")) {
                // Trigger validation on Start Date so required check fires if empty, or comparison fires if set
                $startDate.valid();
            }
        }
    });

    // flag for focused
    $(document).on("focus", "input[type='date']", function () {
        const $this = $(this);
        $(this).data("focused", true);
    });
}

// Clears the existing data on the form!!!
const clearFormData = function (formId) {
    const $form = $(formId);
    // My Code: Clear the existing data on modal form!!!
    $form[0].reset();
    $form.find('input[type="hidden"]').val('');

    // Reset selects (this is the missing piece)
    $form.find('select').each(function () {
        $(this).val('');          // clear value
        $(this).trigger('change'); // update UI (important for Bootstrap)
    });

    // Reset Checkbox
    $form.find('input[type="checkbox"]').each(function () {
        $(this).prop('checked', false);
        $(this).val(0); // Set the value to 0 for unchecked state
        $(this).trigger('change');
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

// Returns today's date in 'YYYY-MM-DD' format for USA
function getTodaysUSDate() {

    const d = new Date(); // local USA time
    const yyyy = d.getFullYear();
    const mm = String(d.getMonth() + 1).padStart(2, '0');
    const dd = String(d.getDate()).padStart(2, '0');

    return `${yyyy}-${mm}-${dd}`;
    
}


// Creates a Disabled Blank Option
const createBlankOptionForSelect = function (blankOptionText, blankOptionDisabled) {

    const blankOption = document.createElement("option");
    blankOption.value = "";
    blankOption.textContent = blankOptionText;
    blankOption.disabled = blankOptionDisabled;
    blankOption.selected = true;
    return blankOption;

}

// Populates the 'select' on the form with the supplied JSON Array of data.
// selectElementName must be element Name NOT Id and It's Name and Case must match the Model's member names.
const populateSelectFromJSON = function (selectElementId, jsonArray, valueField, textField, blankOptionText, blankOptionDisabled = true) {

    var selectElement = document.getElementById(selectElementId);
    // Clear existing options
    selectElement.innerHTML = "";

    // Add blank first option
    if (blankOptionText) {
        selectElement.append(createBlankOptionForSelect(blankOptionText, blankOptionDisabled));
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
    form.querySelectorAll('select, input[type="checkbox"]').forEach(ctrl => {
        const name = ctrl.name;
        const value = (ctrl.tagName === 'INPUT' && ctrl.type === 'checkbox') ? (ctrl.checked ? 1 : 0) : ctrl.value;
        // const value = ctrl.value;
        formResults[name] = value;
        // If FormData skipped it, add it manually
        // if (!formResults.hasOwnProperty(name)) {
            //formResults[name] = value === "" ? "" : value;
        // }
    });

    return formResults;
}


// This function will work if all the elements in the form has name and it is the same as Record Model Element Name & in Same Case
const setFormData = function (form, record){
    // Iterate through each form element
    for (const element of form.elements) {
        // Check if the element has a name attribute (filters out generic buttons)
        if (element.name) {
            element.value = record[element.name] ?? "";
        }
    }
}

// Shows the material-symbols-outlined icons as oneline with the text. This is useful for buttons and other UI elements where you want to combine an icon with a label.
function iconText(icon, text) {
    return `<span style="display:flex; align-items:center; justify-content:center; width:100%;">
                <span class="material-symbols-outlined" style="margin-right:4px;">${icon}</span>
                ${text}
            </span>`;
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

