// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

// Configuration defaults

// Core function: Safely purges event listeners before stripping data
// Changed on: 08/27/2026
// Core function: Safely purges event listeners before stripping data

// Detach the existing validation, Remove the validation data (validation object), clear error messages from the elements, ReBuild & Attach the validation to the form, and
// set the blur effect for validation!!!
function rebindValidation($form) {

    // Unbind internal handlers (.validate namespace)
    $form.off(".validate");

    // Clear validation data caches
    $form.removeData("validator");              // Wipe old rules
    $form.removeData("unobtrusiveValidation");  // Wipe old MVC metadata

    // Clear existing visual validation errors and messages from the DOM
    $form.find('input, select, textarea')
        .removeClass('input-validation-error')
        .removeClass('valid');

    $form.find('[data-valmsg-for]')
        .text('')
        .removeClass('field-validation-error')
        .addClass('field-validation-valid');

    // Re-parse unobtrusive validation rules (Rebuild everything from scratch and attach it to the form)
    if ($.validator && $.validator.unobtrusive) {
        $.validator.unobtrusive.parse($form);
    }

    const validator = $form.data('validator');
    if (validator) {

        // Prevent aggressive default onfocusout validation
        validator.settings.onfocusout = false;

        // Custom blur tracking for single element validation
        $form.off('blur.customVal', 'input, select')
             .on('blur.customVal', 'input, select', function () {
                 validator.element(this);
             });

    }

}


const setUpUnobtrusiveValidationOnModal = function (modalId) {

    $(document).ready(function () {
        
        // Blur active elements when modal closes
        $(modalId).off('hide.bs.modal').on('hide.bs.modal', function () {
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
        const $this = $(this);  // Element who has raised the event!
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


// To validate amount value agaist the Type of Category!
// Returns false, if amount is -ve and category is income and amount is +ve and category is expense, otherwise returns true.
const appendAmountValidationForCategoryType = function (categoryElementName, amountElementName) {
    // Amount Validation
    $.validator.addMethod("isvalidforcategorytype", function (value, element, params) {

        if (!value) return true;

        var $form = $(element).closest("form");

        // Search by name first, then fallback to ID
        var $otherElement = $form.find("[name='" + params + "']");
        if (!$otherElement.length) {
            $otherElement = $form.find("#" + params);
        }

        var targetValue = $otherElement.val();
        if (!targetValue) { // if category is not selected yet! (it's value will be "")
            return true;    // Leave it for required attribute...
        }
        else {

            if ((targetValue == "-1" && parseFloat(value) > 0) || (targetValue == "1" && parseFloat(value) < 0))
                return false;
        };

        return true;

    }, "For expense type of category, amount should be negative(-) otherwise positive(+).");


    // ADD TO UNOBSTRUSIVE VALIDATOR
    $.validator.unobtrusive.adapters.add("isvalidforcategorytype", ["other"], function (options) {
        options.rules["isvalidforcategorytype"] = options.params.other;
        options.messages["isvalidforcategorytype"] = options.message;
    });

    $(document).on("change", "select[name='"+ categoryElementName + "']", function () {
        var $amountElement = $("input[name='"+ amountElementName + "']");
        $amountElement.valid();
    });

}

// Clears the existing data on the form!!!
const clearFormData = function (formId) {

    const $form = $(formId);
    // My Code: Clear the existing data on modal form!!!
    // $form[0].reset();    // REMOVED on 08/27/2026

    // Replace $form[0].reset() with manual clearing so native 'reset' event does not fire: -- ADDED on 08/27/2026
    $form.find('input:not([type="checkbox"]):not([type="radio"]), textarea').val('');

    // $form.find('input[type="hidden"]').val('');  // REMOVED on 08/27/2026 as above line clears all inputs except checkboxes and radios, so hidden inputs are also cleared.

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

// Updates the paginationJSON object with new start and end record numbers based upon the navigationRequest ('first', 'prev', 'next', 'last') and returns the updated object!!!
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

// BULK IMPORT VALIDATIONS - START HERE...

// Checks if the amount is valid and in 2 digits!!!
const isValidAmount = function (amount) {

  // Guard against non-strings, null, undefined, or empty/whitespace values
  if (typeof amount !== 'string' || !amount.trim()) {
    return false;
  }

  // Regex: Optional negative sign, digits, max 2 decimal places
  const amountRegex = /^-?\d+(\.\d{1,2})?$/;
  if (!amountRegex.test(amount.trim())) {
    return false;
  }

  // Ensure it parses cleanly as a number
  const numericValue = parseFloat(amount);
  return !isNaN(numericValue);

}

/*
// Checks if the date string is in a valid format (M/D/YYYY or MM/DD/YYYY) and does not accept blank, empty, or null values, and 
// represents a real calendar date, including leap years and month lengths.
const isValidDate2 = function (dateString) {

    // Reject non-strings, empty strings, or whitespace-only strings
    if (typeof dateString !== 'string' || !dateString.trim()) {
        return false;
    }

    // Check format using Regular Expression (allows /, ., or - as separators)
    const regex = /^(0?[1-9]|1[0-2])[\/.-](0?[1-9]|[12][0-9]|3[01])[\/.-]\d{4}$/;

    if (!regex.test(dateString)) {
        return false;
    }

    // Parse string into integers by splitting on any separator
    const parts = dateString.split(/[\/.-]/);
    const month = parseInt(parts[0], 10);
    const day = parseInt(parts[1], 10);
    const year = parseInt(parts[2], 10);

    // Check calendar validity (handles leap years and month lengths)
    const dateObj = new Date(year, month - 1, day);

    return (
        dateObj.getFullYear() === year &&
        dateObj.getMonth() === month - 1 &&
        dateObj.getDate() === day
    );
}
    */

const isValidDate2 = function (dateString) {

    // Reject non-strings, empty strings, or whitespace-only strings
    if (typeof dateString !== 'string' || !dateString.trim()) {
        return false;
    }

    // Check format (allows 2 or 4 digit years with /, ., or - as separators)
    const regex = /^(0?[1-9]|1[0-2])[\/.-](0?[1-9]|[12][0-9]|3[01])[\/.-](\d{4}|\d{2})$/;
    if (!regex.test(dateString)) {
        return false;
    }

    // Parse string into integers by splitting on any separator
    const parts = dateString.split(/[\/.-]/);
    const month = parseInt(parts[0], 10);
    const day = parseInt(parts[1], 10);
    let year = parseInt(parts[2], 10);

    // Convert 2-digit year to 4-digit year (optional pivot, e.g., < 50 = 20xx, >= 50 = 19xx)
    if (parts[2].length === 2) {
        year += year < 50 ? 2000 : 1900;
    }

    // Check calendar validity (handles leap years and month lengths)
    const dateObj = new Date(year, month - 1, day);
    dateObj.setFullYear(year); // Ensures 4-digit year isn't overwritten by JS 1900s fallback

    return (
        dateObj.getFullYear() === year &&
        dateObj.getMonth() === month - 1 &&
        dateObj.getDate() === day
    );
};

// BULK IMPORT VALIDATIONS - ENDS HERE...


// Checks if it's a valid date
// value: The date 'string' to validate (in 'YYYY-MM-DD' format) (MUST BE FOR THE BELOW FUNCTION TO WORK CORRECTLY!!!)
const isValidDate  = function (value) {

    const d = new Date(value);

    return (
        value !== "" &&                // not empty
        !isNaN(d.getTime()) &&        // Date object is valid
        value === d.toISOString().split("T")[0] // matches YYYY-MM-DD format
        // value === formatDateToYYYYMMDD(d)        // WRONG...WILL FAIL
    );

}

// Returns today's date in 'YYYY-MM-DD' format for USA
const formatDateToYYYYMMDD = function (d) {

    const yyyy = d.getFullYear();
    const mm = String(d.getMonth() + 1).padStart(2, '0');
    const dd = String(d.getDate()).padStart(2, '0');

    return `${yyyy}-${mm}-${dd}`;
    
}

// Get the date object from a 'YYYY-MM-DD' formatted string
const parseLocalDate = function (yyyyMmDd) {

    const [y, m, d] = yyyyMmDd.split("-");
    return new Date(Number(y), Number(m) - 1, Number(d));

}

// Formats a date string to a local format (like "mm/dd/yyyy" for en-US, "dd/mm/yyyy" for en-GB, "yyyy-mm-dd" for ja-JP, etc.)
const formatToLocalDate = function (yyyyMmDd, locale) {
    
    const d = parseLocalDate(yyyyMmDd);
    return d.toLocaleDateString(locale);

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
const getFormEntries = function (formId) {

    const form = document.getElementById(formId);

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
const setFormData = function (formId, record){

    const form = document.getElementById(formId);
    // Iterate through each form element
    for (const element of form.elements) {
        // Check if the element has a name attribute (filters out generic buttons)
        if (element.name) {
            element.value = record[element.name] ?? "";
        }
    }

}

// Shows the material-symbols-outlined icons as oneline with the text. This is useful for buttons and other UI elements where you want to combine an icon with a label.
const iconText = function (icon, text) {

    return `<span style="display:flex; align-items:center; justify-content:center; width:100%;">
                <span class="material-symbols-outlined" style="margin-right:4px;">${icon}</span>
                ${text}
            </span>`;

}

// Displays the message in the 'dbMessage' div, TO BE USED FOR DATABASE OPERATION RESULT!!!
const showDbMessage = function (message, isSuccess = true) {

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

// for bootstrap pop-over required!!!
document.addEventListener("DOMContentLoaded", function () {

    document.querySelectorAll('[data-bs-toggle="popover"]')
        .forEach(el => new bootstrap.Popover(el));

});
