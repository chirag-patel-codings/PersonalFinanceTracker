let csvUploadAccountIdValue = '';
let csvFileContentAsData = null;
let currentBulkImportTemplate = {};
let validationErrors = [];

let currentStep = 'uploadFile';  // Default to 'Step 1'


// Resize Modal 
const resizeModal = function () {

    const modalElement = document.getElementById('csvFileUploadModal');
    const modalDialog = modalElement.querySelector('.modal-dialog');

    modalDialog.classList.remove('modal-lg', 'modal-xl');   // 'modal-sm', 'modal-md', 

    if(currentStep == 'uploadFile'){
        modalDialog.classList.add('modal-lg');
    }
    else{
        modalDialog.classList.add('modal-xl');
    }

    // Force reflow by reading a layout property
    void modalDialog.offsetHeight;

}

// Change the value of currentStep - Start

function handleStepChange() {
    
    currentStep = this.id;
    resizeModal();
}

document.querySelectorAll('button.nav-link').forEach(button => {
    button.addEventListener('click', handleStepChange);
    // button.addEventListener('focus', handleStepChange);  // Does not work correctly!
});

// Change the value of currentStep - End


// Enable or Disable the Next Step button based on the current step and conditions
const enableDisableNextStepButton = function (currentStep) {

    const nextStepButtonId = currentStep == 'uploadFile' ? 'btnStep1Next' : 'btnStep2Next';
    const nextStepButton = document.getElementById(nextStepButtonId);

    switch (currentStep) {
        case 'uploadFile':
            
            if (csvUploadAccountIdValue != '' 
                    && csvFileContentAsData != null) {
                nextStepButton.disabled = false;
            }
            else{
                nextStepButton.disabled = true;
            }
            
            break;
    }

}


// Show or Hide the CSV File Upload Modal
const showHideCSVFileUploadModal = function (show=true) {

    const csvFileUploadModalElement = document.getElementById('csvFileUploadModal');

    // Get the existing instance, or create a new one if it doesn't exist yet
    let modalInstance = bootstrap.Modal.getInstance(csvFileUploadModalElement);

    if (!modalInstance) {
        modalInstance = new bootstrap.Modal(csvFileUploadModalElement);
    }

    if(show){

        populateSelectFromJSON('csvUploadAccountId', accountsListJSON, 'listOptionId', 'listOptionName', '-- Select Account --');
        document.getElementById('csvUploadAccountId').value = csvUploadAccountIdValue;  // Set the previously selected account if any
        getBulkImportTemplateNames();
        modalInstance.show();

    }
    else{

        modalInstance.hide();

    }

}

// Returns the Mapped column select values and column index as javascript object.
const getSelectedColumnMappingsObject = function(){

    const selectedColumnMappingObject = {};

    document.querySelectorAll('#csvDataColumnMappingHeader select').forEach(select => {
        if(select.selectedIndex != 0){
            selectedColumnMappingObject[select.options[select.selectedIndex].text] = select.getAttribute('data-col-index');
        }
    });

    return selectedColumnMappingObject;

}

// Display the CSV File Data from Specific Header Row in the table for mapping and preview
const displayCSVFileData = function (headerRowIndex=0, isForDataPreview = false) {
    
    let rowIndex = headerRowIndex;
    let selectedColumnMappingObject = getSelectedColumnMappingsObject();

    const csvDataTableNameId = isForDataPreview ? 'csvPreviewDataTable' : 'csvDataColumnMappingTable';
    const csvDataTable = document.getElementById(csvDataTableNameId);

    const csvDataColumnMappingHeaderId = isForDataPreview ? 'csvPreviewDataTableHeader' : 'csvDataColumnMappingHeader';
    const csvDataColumnMappingHeader = document.getElementById(csvDataColumnMappingHeaderId);

    const csvDataColumnMappingBodyId = isForDataPreview ? 'csvPreviewDataTableBody' : 'csvDataColumnMappingBody';
    const csvDataColumnMappingBody = document.getElementById(csvDataColumnMappingBodyId);

    csvDataColumnMappingHeader.innerHTML = "";  // Clear previous header
    csvDataColumnMappingBody.innerHTML= "";

    let headerRow1 = document.createElement('tr');
    let rowData = csvFileContentAsData[rowIndex];
    let colWidthPercentage = Math.floor(95 / (rowData.length-1));

    // First Column
    let firstColumnHeader = document.createElement('th');
    firstColumnHeader.textContent = 'Id';
    firstColumnHeader.setAttribute('rowspan', isForDataPreview ? '1' : '2');
    firstColumnHeader.style.setProperty('--w', '5' + '%');
    firstColumnHeader.classList.add('dynamic-width');
    headerRow1.appendChild(firstColumnHeader);
    
    // First Header Row
    if(isForDataPreview){

        for(colIndex = 0; colIndex < Object.keys(selectedColumnMappingObject).length; colIndex++){

            let dataColumnHeader = document.createElement('th');
            dataColumnHeader.style.setProperty('width', colWidthPercentage + '%', 'important');
            dataColumnHeader.classList.add('dynamic-width');
            dataColumnHeader.textContent = Object.keys(selectedColumnMappingObject)[colIndex];;
            headerRow1.appendChild(dataColumnHeader);

        }

    }
    else{

        for(colIndex = 0; colIndex < rowData.length; colIndex++){

            let dataColumnHeader = document.createElement('th');
            let selectContainerDiv = document.createElement('div');
            selectContainerDiv.className='col-sm field p-1';
            // dataColumnHeader.style.width = colWidthPercentage + '%';
            dataColumnHeader.style.setProperty('width', colWidthPercentage + '%', 'important');
            dataColumnHeader.classList.add('dynamic-width');
            let newSelect = generateDataMappingSelect(colIndex, rowData[colIndex]);
            // Change the selected index if already existing!!!
            headerRow1.querySelectorAll('select').forEach(select => {
                if(select.selectedIndex == newSelect.selectedIndex)
                    newSelect.selectedIndex = 0;
            });
            selectContainerDiv.appendChild(newSelect);
            dataColumnHeader.appendChild(selectContainerDiv);
            headerRow1.appendChild(dataColumnHeader);

        }

    }
    csvDataColumnMappingHeader.appendChild(headerRow1);

    // Add a second header row if not "Preview" Mode!
    if (!isForDataPreview) {
        let headerRow2 = document.createElement('tr');
        // Second Header Row
        for (colIndex = 0; colIndex < rowData.length; colIndex++) {
            let dataColumnHeader = document.createElement('th');
            dataColumnHeader.textContent = rowData[colIndex];
            // dataColumnHeader.style.width = colWidthPercentage + '%';
            headerRow2.appendChild(dataColumnHeader);
        }
        csvDataColumnMappingHeader.appendChild(headerRow2);
    }

    csvDataTable.appendChild(csvDataColumnMappingHeader);

    // Data Values Rows
    rowIndex = rowIndex + 1;
    recordIndex = 1;  // Set the record index for the data rows
    // let dataRowsCount = csvFileContentAsData.length + 1;
    for(; rowIndex < csvFileContentAsData.length; rowIndex++){
        let dataRow = document.createElement('tr');
        let rowData = csvFileContentAsData[rowIndex];

        // First Column
        let idColumn = document.createElement('td');
        // idColumn.style.width = colWidthPercentage + '% !important';
        // idColumn.style.setProperty('width', colWidthPercentage + '%', 'important');
        idColumn.style.textAlign = 'center';
        idColumn.style.verticalAlign = 'middle';
        idColumn.textContent = recordIndex;
        dataRow.appendChild(idColumn);

        for(dataColIndex = 0; dataColIndex < rowData.length; dataColIndex++){
            let dataColumn = document.createElement('td');
            // dataColumn.style.setProperty('width', colWidthPercentage + '%', 'important');
            if(isForDataPreview){
                if(Object.values(selectedColumnMappingObject).includes(dataColIndex.toString())){
                    dataColumn.textContent = rowData[dataColIndex];
                }
                else{   // skip the dataColumn Append
                    continue;
                }
            }
            else{
                dataColumn.textContent = rowData[dataColIndex];
            }
            dataRow.appendChild(dataColumn);
        }

        csvDataColumnMappingBody.appendChild(dataRow);
        csvDataTable.appendChild(csvDataColumnMappingBody);
        recordIndex++;
    }

}

// Generates the dropdown select element for mapping CSV columns to transaction fields
const generateDataMappingSelect = function(dataColIndex, colText){

    const dataMappingSelect = document.createElement('select');
    dataMappingSelect.id = 'dataMappingSelect-' + dataColIndex;
    dataMappingSelect.name = 'dataMappingSelect-' + dataColIndex;
    dataMappingSelect.style.width = '100%';
    dataMappingSelect.className = 'form-control';
    dataMappingSelect.setAttribute('data-col-index', dataColIndex); // will help to find the column index in CSV file data...
    let templateElementsLength = Object.keys(currentBulkImportTemplate).length; // Will not be more than 3 if no template selected!!

    // Add dropdown options and set active selection conditionally
    const defaultOpt = new Option('--Ignored--', '-1');
    defaultOpt.selected = true;  // Set the default option as selected
    const dateOpt = new Option('Date', '0');
    dateOpt.selected = templateElementsLength > 3 ? currentBulkImportTemplate.dateFieldIndex == dataColIndex : colText.toLowerCase().includes('date');  // Set the first option as selected by default
    const descOpt = new Option("Description", '1');
    descOpt.selected = templateElementsLength > 3 ? currentBulkImportTemplate.descriptionFieldIndex == dataColIndex : colText.toLowerCase().includes('description');  // Set the second option as selected by default
    const amountOpt = new Option("Amount", '2');
    amountOpt.selected = templateElementsLength > 3 ? currentBulkImportTemplate.amountFieldIndex == dataColIndex : colText.toLowerCase().includes('amount');  // Set the third option as selected by default
    const debitOutflowOpt = new Option("Debit/Outflow", '3');
    debitOutflowOpt.selected = templateElementsLength > 3 ? currentBulkImportTemplate.debitFieldIndex == dataColIndex : (colText.toLowerCase().includes('debit') || colText.toLowerCase().includes('outflow'));  // Set the fourth option as selected by default
    const creditInflowOpt = new Option("Credit/Inflow", '4');
    creditInflowOpt.selected = templateElementsLength > 3 ? currentBulkImportTemplate.creditFieldIndex == dataColIndex : (colText.toLowerCase().includes('credit') || colText.toLowerCase().includes('inflow'));  // Set the fifth option as selected by default

    dataMappingSelect.add(defaultOpt);
    dataMappingSelect.add(dateOpt);
    dataMappingSelect.add(descOpt);
    dataMappingSelect.add(amountOpt);
    dataMappingSelect.add(debitOutflowOpt);
    dataMappingSelect.add(creditInflowOpt);
    
    return dataMappingSelect;

}
// Formats the error messages in the form of an unordered list
// Send the messages like this: generateMessagesAsUOList("Error 1", "Error 2", "Error 3") -- OR -- generateMessagesAsUOList(["Error 1", "Error 2", "Error 3"])
// But if you have used the function definition like "const generateMessagesAsUOList = function (...messages)" then calling the generateMessagesAsUOList("Error 1", "Error 2", "Error 3") is fine 
// but, if the argument is an array then you must call the function as generateMessagesAsUOList(...["Error 1", "Error 2", "Error 3"]) 
const generateMessagesAsUOList = function (messages){
    
    messages = Array.isArray(messages) ? messages : [messages];

    let uoList = '<ul>';
    messages.forEach(m => {
        uoList += `<li>${m}</li>`;
    });
    uoList += '</ul>';

    return uoList;

}

// Displays the CSV Validation Errors in the UI
const displayCSVValidationErrors = function (errors, recordDetails){

    let csvFileValidationErrors = document.getElementById('csvFileValidationErrors');

    csvFileValidationErrors.innerHTML = "";

    if(recordDetails){
        csvFileValidationErrors.innerHTML = recordDetails;
    }

    if (errors){
        csvFileValidationErrors.innerHTML += generateMessagesAsUOList(errors) ;
    }

}


// Clears the existing values of the 'mapFieldsTab' elements
const clearMapFieldsTabContents = function(){

    document.getElementById('csvImportSavedTemplatesSelect').value = "";
    document.getElementById('headerRow').value = 0;

    /* BELOW WOULD BE AUTOMATICALLY DONE WHEN A VALID FILE IS SELECTED
    // document.getElementById('csvDataColumnMappingHeader').innerHTML = "";
    // document.getElementById('csvDataColumnMappingBody').innerHTML = "";
    */
}

// Enable or Disable CSV File Upload Tabs:
const enableDisableCSVUploadTabs = function (elementId, enable=true){
    
    const csvUploadTab = document.getElementById(elementId);

    if(enable){
        csvUploadTab.removeAttribute("disabled");
    }
    else{

        clearMapFieldsTabContents();
        csvUploadTab.setAttribute("disabled", "disabled");
    }

}

// Returns the 'data-col-index' value for the element that contains the searchValue
const getDataColIndex = function(searchValue){

    let result = -1;
    const selects = document.querySelectorAll('#csvDataColumnMappingHeader select');
    selects.forEach(select => {
        if (select.options[select.selectedIndex].text == searchValue) {
            result = parseInt(select.getAttribute('data-col-index'));
        }
    });

    return result;

}

// Populates the 'currentBulkImportTemplate' members with appropriate values
const getImportTemplateValues = function () {

    currentBulkImportTemplate.importTemplateName = document.getElementById('csvImportSavedTemplatesSelect').value;
    currentBulkImportTemplate.accountId = csvUploadAccountIdValue;
    currentBulkImportTemplate.headerRowIndex = document.querySelector('select[name="headerRow"]').value;
    currentBulkImportTemplate.dateFieldIndex = getDataColIndex('Date');
    currentBulkImportTemplate.descriptionFieldIndex = getDataColIndex('Description');
    currentBulkImportTemplate.amountFieldIndex = getDataColIndex('Amount');
    currentBulkImportTemplate.debitFieldIndex = getDataColIndex('Debit/Outflow');
    currentBulkImportTemplate.creditFieldIndex = getDataColIndex('Credit/Inflow');

}

/*
// Selects the specified value for the specified 'select'
const pickValueFromSelectOptions = function (select, valueToPick) {
    select.value = Array.from(select.options).find(o => o.text === valueToPick).value;
}

// Applies the import Template data to elements on 'mapFieldsTab' modal tab
const applyImportTemplateValues = function () {
    
    const headerRowSelect = document.querySelector('select[name="headerRow"]');
    const dateColumnSelect = document.querySelector('select[data-col-index="' + currentBulkImportTemplate.dateFieldIndex + '"]');
    const descriptionColumnSelect = document.querySelector('select[data-col-index="' + currentBulkImportTemplate.descriptionFieldIndex + '"]');

    // RESET THE VALUES FIRST!!!
    headerRowSelect.value = 0;
    let columnSelects = document.querySelectorAll('#csvDataColumnMappingHeader select');
    columnSelects.forEach(select => {
        select.selectedIndex = 0;
    });

    if (currentBulkImportTemplate.dateFieldIndex > -1 && currentBulkImportTemplate.descriptionFieldIndex > -1) {

        headerRowSelect.value = currentBulkImportTemplate.headerRowIndex;
        pickValueFromSelectOptions(dateColumnSelect, 'Date');
        pickValueFromSelectOptions(descriptionColumnSelect, 'Description');

        if(currentBulkImportTemplate.amountFieldIndex !== '-1'){
            const amountColumnSelect = document.querySelector('select[data-col-index="' + currentBulkImportTemplate.amountFieldIndex + '"]');
            pickValueFromSelectOptions(amountColumnSelect, 'Amount');
        }
        if(currentBulkImportTemplate.debitFieldIndex !== '-1'){
            const debitColumnSelect = document.querySelector('select[data-col-index="' + currentBulkImportTemplate.debitFieldIndex + '"]');
            pickValueFromSelectOptions(debitColumnSelect, 'Debit/Outflow');
        }

        if(currentBulkImportTemplate.creditFieldIndex !== '-1'){
            const creditColumnSelect = document.querySelector('select[data-col-index="' + currentBulkImportTemplate.creditFieldIndex + '"]');
            pickValueFromSelectOptions(creditColumnSelect, 'Credit/Inflow');
        }
    
    }

}
*/


// Gets the template details for supplied 'Template Name' saved for current user
// And applies those values in appropriate form controls
const getBulkImportTemplate = function (importTemplateName) {

    const headerRowSel = document.getElementById('headerRow');

    fetch('/Transaction/GetBulkImportTemplate', {
        method: 'POST',
        credentials: "include",
        headers: {
            "Content-Type": "application/json",
            "RequestVerificationToken": _token
        },
        body: JSON.stringify({ importTemplateName: String(importTemplateName) })
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
            currentBulkImportTemplate = data.importTemplate[0];
            // applyImportTemplateValues();
            headerRowSel.value = currentBulkImportTemplate.headerRowIndex;
            headerRowSel.dispatchEvent((new Event("change")));
        })
        .catch(error => {
            console.error("Fetch operation failed:", error);
        });

}


// Populates the list with Saved Import Template Names
const populateCSVImportSavedTemplatesList = function (bulkImportTemplateNames) {

    const datalist = document.getElementById('csvImportSavedTemplatesList');

    // Clear any existing options first if needed
    datalist.innerHTML = '';

    // Loop through your items and create options
    bulkImportTemplateNames.forEach(templateName => {
        const option = document.createElement('option');
        option.value = templateName; // The text shown in the input/dropdown

        datalist.appendChild(option);
    });
}

// Gets all the templates saved for current user
const getBulkImportTemplateNames = function () {

    fetch('/Transaction/GetBulkImportTemplateNames', {
        method: 'POST',
        credentials: "include",
        headers: {
            "Content-Type": "application/json",
            "RequestVerificationToken": _token
        }
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
            populateCSVImportSavedTemplatesList(data.bulkImportTemplateNames);
            
        })
        .catch(error => {
            console.error("Fetch operation failed:", error);
        });

}

// Delete current record!!
const deleteBulkImportTemplateRecord = function () {

    fetch("/Transaction/DeleteBulkImportTemplate", {
        method: "POST",
        credentials: "include",   // REQUIRED for antiforgery cookie
        body: JSON.stringify({bulkImportTemplateId: currentBulkImportTemplate.importTemplateName}),
        headers: {
            "Content-Type": "application/json",
            "RequestVerificationToken": _token
        }
    })
        .then(res => res.json())
        .then(result => {
            if (result.success) {
                // Delete the template name from the list
                const dataListOptions =  document.getElementById('csvImportSavedTemplatesList').querySelectorAll('option');
                Array.from(dataListOptions).find(opt => opt.value.toLowerCase() === currentBulkImportTemplate.importTemplateName.toLowerCase()).remove();
                document.getElementById('csvImportSavedTemplatesSelect').value = '';
                showDbMessage("Template deleted successfully.", true);
            }
            else {
                console.error(result.message);
                showDbMessage("Error: Template has not been deleted successfully!!!", false);
                return;
            }

        });

}

// Save Current Record or Add New Record!!
const saveBulkImportTemplateRecord = function () {

    currentBulkImportTemplate.headerRowIndex = +currentBulkImportTemplate.headerRowIndex;
    currentBulkImportTemplate.importType = +currentBulkImportTemplate.importType;

    fetch("/Transaction/SaveBulkImportTemplate", {
        method: "POST",
        credentials: "include",   // REQUIRED for antiforgery cookie
        body: JSON.stringify(currentBulkImportTemplate),
        headers: {
            "Content-Type": "application/json",
            "RequestVerificationToken": _token
        }
    })
        .then(res => res.json())
        .then(result => {
            if (result.success) {
                // Add the new template name to list
                const option = document.createElement('option');
                option.value = currentBulkImportTemplate.importTemplateName;
                document.getElementById('csvImportSavedTemplatesList').appendChild(option);

                showDbMessage("Template saved successfully.", true);
            }
            else {
                console.error(result.message);
                showDbMessage("Error: Template has not been saved successfully!!!", false);
                return;
            }

        });

}

// Uploads the CSV file to the server and gets the contents back as JSON
const uploadCSVFileAndGetContents = function(){

    const input = document.getElementById("csvUploadFileInput");
    const csvFile = input.files[0];

    const formData = new FormData();
    formData.append("file", csvFile);

    fetch('/Transaction/UploadCsvFileAndGetContents', {
        method: 'POST',
        credentials: "include",   // REQUIRED for antiforgery cookie
        headers: {
            "RequestVerificationToken": _token
        },
        body: formData
    })
        .then(response => {
            // Check if the HTTP status code is 200-299 (Ok)
            if (!response.ok) {
                // IMPORTANT: return the promise
                return response.json().then(error => {
                    csvFileContentAsData = null;  // Reset the csvFileContentAsData to null if error occurs
                    displayCSVValidationErrors(error.message);
                    
                    enableDisableNextStepButton(currentStep);
                    
                });
            }
            // Parse the body text as JSON
            return response.json();
        })
        .then(data => {
            
            // If error occurred, data will be undefined
            if (!data) return;

            displayCSVValidationErrors(null);   // Clear any previous validation errors
            csvFileContentAsData = data;
            enableDisableNextStepButton(currentStep);
            // clearMapFieldsTabContents();

        })
        .catch(error => {
            console.error("Fetch operation failed:", error);
        });

        // Disable other tabs when there is a new file upload!
        enableDisableCSVUploadTabs('mapFields', false);
        enableDisableCSVUploadTabs('previewDataTab', false);

}


// Uploads the CSV file to the server and gets the contents back as JSON
const ImportCsvFileData = function(){

    currentBulkImportTemplate.headerRowIndex = Number(currentBulkImportTemplate.headerRowIndex);
    currentBulkImportTemplate.importType = Number(currentBulkImportTemplate.importType ?? 0);

    validationErrors.length = 0;

    // currentBulkImportTemplate.importTemplateName = "Temp";

    fetch('/Transaction/ImportCsvFileData', {
        method: 'POST',
        credentials: "include",   // REQUIRED for antiforgery cookie
        headers: {
            "Content-Type": "application/json",
            "RequestVerificationToken": _token
        },
        body: JSON.stringify(currentBulkImportTemplate)
    })
        .then(response => {
            
            // Parse the body text as JSON
            return response.json();
        })
        .then(result => {
            if (result.success) {
                showHideCSVFileUploadModal(false);
                displayCSVValidationErrors(null);
                // doNavigation(null, 'current');  // Refresh the page!
                doNavigation(null, 'first', true);
                showDbMessage("Data import sucessful.", true);
            }
            else {
                console.error(result.message);
                result.message.forEach(e => validationErrors.push(e.errorMessage));
                displayCSVValidationErrors(validationErrors, result.recordDetails);
                //showDbMessage("Error: Data has not been imported successfully!!!", false);
                
                return;
            }

        })
        .catch(error => {
            console.error("Fetch operation failed:", error);
        });

        // Disable other tabs when there is a new file upload!
        enableDisableCSVUploadTabs('mapFields', false);
        enableDisableCSVUploadTabs('previewDataTab', false);

}

// Validates the selected header field mappings and returns an array of error messages if any required fields are not mapped
const validateHeaderFieldSelection = function () {

    let isValid = true;
    validationErrors.length = 0;    // This instantly clears the original array and affects all references pointing to it.

    // First check if the required columns are selected in the mapping dropdowns
    if (currentBulkImportTemplate.dateFieldIndex == -1){
        validationErrors.push("Please select the 'Date' column for the mapping.");
        isValid = false;
    }
    if (currentBulkImportTemplate.descriptionFieldIndex == -1){
        validationErrors.push("Please select the 'Description' column for the mapping.");
        isValid = false;
    }
    if(currentBulkImportTemplate.amountFieldIndex == -1 && 
            (currentBulkImportTemplate.debitFieldIndex == -1 || currentBulkImportTemplate.creditFieldIndex == -1)){
        validationErrors.push("Please select the 'Amount' column for the mapping. Either 'Amount' or both 'Debit/Outflow' and 'Credit/Inflow' columns are required.");
        isValid = false;
    }

    return isValid;

}

// Validate all the data and header selection
const isValidCSVFileData = function (dataHeaderRowIndex = 0) {

    validationErrors.length = 0;    // Clear any existing error!!!
    getImportTemplateValues();
    
    let startRowIndex = +(document.getElementById('headerRow').value) + 1; // value starts at 0;
    let dateColumnIndex = getDataColIndex('Date');
    let descriptionColumnIndex = getDataColIndex('Description');
    let amountColumnIndex = getDataColIndex('Amount');
    let debitColumnIndex = getDataColIndex('Debit/Outflow');
    let creditColumnIndex = getDataColIndex('Credit/Inflow');
    let transactionDescriptionLength = '';

    if (validateHeaderFieldSelection()) {

        for (i = startRowIndex; i < csvFileContentAsData.length; i++) {

            if (!isValidDate2(csvFileContentAsData[i][dateColumnIndex])) {
                validationErrors.push('Date value is NOT valid at line ... ' + (i + 1) + ' (in actual data file!)');
                break;
            }
            transactionDescriptionLength = csvFileContentAsData[i][descriptionColumnIndex].trim().length;
            if ( transactionDescriptionLength == 0 || transactionDescriptionLength > 255) {
                validationErrors.push('Description value is NOT valid at line ... ' + (i + 1) + ' (in actual data file!)');
                break;
            }

            amountValueColumnIndex = amountColumnIndex != -1 ? amountColumnIndex : (debitColumnIndex != -1 ? debitColumnIndex : creditColumnIndex);
            
            if (!isValidAmount(csvFileContentAsData[i][amountValueColumnIndex])) {
                validationErrors.push('Amount value is NOT valid at line ... ' + (i + 1) + ' (in actual data file!)');
                break;
            }

        }

    }

    displayCSVValidationErrors(validationErrors);

    if (validationErrors.length > 0) {
        return false;  // Stop further validation if required columns are not selected
    }
    else {
        return true;
    }

}

// Checks if the uploaded file is a valid CSV file by checking its magic numbers and content
async function isValidCSVFile(file) {

    if (!file || file.size === 0) return false;

    // Check magic numbers (file signatures) for common non-CSV formats
    const signatureSlice = file.slice(0, 8);
    const buffer = await signatureSlice.arrayBuffer();
    const bytes = new Uint8Array(buffer);

    // PDF (%PDF)
    if (bytes[0] === 0x25 && bytes[1] === 0x50 && bytes[2] === 0x44 && bytes[3] === 0x46) return false;

    // ZIP / Office documents (.xlsx, .docx, .zip) (PK)
    if (bytes[0] === 0x50 && bytes[1] === 0x4B) return false;

    // PNG (\x89PNG)
    if (bytes[0] === 0x89 && bytes[1] === 0x50 && bytes[2] === 0x4E && bytes[3] === 0x47) return false;

    // JPEG (FF D8 FF)
    if (bytes[0] === 0xFF && bytes[1] === 0xD8 && bytes[2] === 0xFF) return false;

    // Windows Executable / DLL (MZ)
    if (bytes[0] === 0x4D && bytes[1] === 0x5A) return false;

    // Read a text sample to check for binary data
    const textSlice = file.slice(0, 2048);
    let text;

    try {
        text = await textSlice.text();
    } catch (err) {
        return false; // If it can't be decoded as text, it's not a valid CSV
    }

    // Null bytes are a definitive indicator of binary files
    if (text.includes('\0')) return false;

    // Ensure the file isn't completely empty after trimming
    if (text.trim().length === 0) return false;

    return true;

}

// Event listener for CSV Upload Account 'select'
document.getElementById('csvUploadAccountId').addEventListener('change', function (event) {
    event.preventDefault();
    csvUploadAccountIdValue = this.value;
    enableDisableNextStepButton(currentStep);
});

// Import the data
document.getElementById('btnImportCSVData').addEventListener('click', function (event) {
    event.preventDefault();
    ImportCsvFileData();
});

// Event Listener for HeaderRow 'select' 
document.getElementById('headerRow').addEventListener('change', function (event) {
    
    event.preventDefault();
    displayCSVFileData(parseInt(this.value), false);

    currentBulkImportTemplate.headerRowIndex = this.value;
    
});

// Show the modal when the "Upload CSV" button is clicked
document.getElementById('showCSVUploadModal').addEventListener('click', function (event) {
    event.preventDefault();
    showHideCSVFileUploadModal(true);
});


// IMPORT TEMPLATE - START
document.getElementById('csvImportSavedTemplatesSelect').addEventListener('blur', function(event){

    const saveCSVImportTemplate = document.getElementById('saveCSVImportTemplate');
    const deleteCSVImportTemplate = document.getElementById('deleteCSVImportTemplate');
    const dataListOptions =  document.getElementById('csvImportSavedTemplatesList').querySelectorAll('option');

    event.preventDefault();
    let currentValue = this.value.trim();

    if (currentValue == ''){
        
        saveCSVImportTemplate.setAttribute('disabled', 'disabled');
        deleteCSVImportTemplate.setAttribute('disabled', 'disabled');
    }
    else{
        getBulkImportTemplate(currentValue);
        saveCSVImportTemplate.removeAttribute('disabled');
        if(Array.from(dataListOptions).find(opt => opt.value.toLowerCase() === currentValue.toLowerCase())){
            deleteCSVImportTemplate.removeAttribute('disabled');
        }
        else{
            deleteCSVImportTemplate.setAttribute('disabled', 'disabled');
        }
    }

});

// Save the TEMPLATE for CSV Data File Header Fields Mappings
document.getElementById('saveCSVImportTemplate').addEventListener('click', function(event){

    event.preventDefault();
    getImportTemplateValues();
    let requiredFieldsSelected = validateHeaderFieldSelection();

    if (requiredFieldsSelected){
        displayCSVValidationErrors(null); // Clear errors
        saveBulkImportTemplateRecord();
    }
    else{
        displayCSVValidationErrors(validationErrors);   // Display errors
    }

});

// Delete Existing Template!
document.getElementById('deleteCSVImportTemplate').addEventListener('click', function(event){
    
    event.preventDefault();
    if(confirm("Are you sure, you want to delete the template?")){
        deleteBulkImportTemplateRecord();

    }
    
});

// Disable preview data tab...
document.getElementById('mapFieldsTab').addEventListener('change', function (event) {

    if (event.target && event.target.tagName === 'SELECT') {
        document.getElementById('previewData').disabled = true;
    }

});

// Change other element's value to '--Ignored--'
document.getElementById('csvDataColumnMappingHeader').addEventListener('change', function (event) {
    
    // Check if the changed element is a <select>
    if (event.target && event.target.tagName === 'SELECT') {
        const changedSelect = event.target;
        const currentDataColIndex = changedSelect.getAttribute('data-col-index');

        // Query all select elements inside the container dynamically
        const allSelects = document.getElementById('csvDataColumnMappingHeader').querySelectorAll('select');

        allSelects.forEach(selectEle => {
            // Reset duplicate selections in other columns
            if (
                selectEle.getAttribute('data-col-index') !== currentDataColIndex &&
                selectEle.selectedIndex === changedSelect.selectedIndex &&
                changedSelect.selectedIndex !== 0 // Optional: avoid resetting if set to default/placeholder
            ) {
                selectEle.selectedIndex = 0;
            }
        });
    }

});

// IMPORT TEMPLATE - END

// File Upload: Drag & Drop + Click -- Start
const dz = document.getElementById("dropzone");
const input = dz.querySelector("input");

// Map the event so click event acts as input element's click
dz.addEventListener("click", () => input.click());

dz.addEventListener("dragleave", () => {

    dz.style.background = "";

});

// Delegate dragover/drop to parent
["dragover", "drop"].forEach(evt => {
    dz.addEventListener(evt, e => {
        e.preventDefault();
        e.stopPropagation();
    });
});

dz.addEventListener("drop", e => {
    input.files = e.dataTransfer.files;
    input.dispatchEvent(new Event("change"));
});


// Event listener for file input change...Fires whenever a file is selected/changed either by clicking or drag & drop
document.getElementById('csvUploadFileInput').addEventListener('change', async function (event) {
    
    const file = event.target.files[0];
    
    const isValidCSV = await isValidCSVFile(file);

    const fileNameDisplayPEle = document.getElementById('fileNameDisplay');

    if (!this.value.toLowerCase().endsWith(".csv") || !isValidCSV) {
        csvFileContentAsData = null;  // Reset the csvFileContentAsData to null if error occurs
        displayCSVValidationErrors("Only non-empty valid .CSV files are allowed!");
        enableDisableNextStepButton(currentStep);
        enableDisableCSVUploadTabs('mapFields', false);
        enableDisableCSVUploadTabs('previewDataTab', false);
        this.value = "";
        fileNameDisplayPEle.textContent = "";
        return;
    }


    fileNameDisplayPEle.textContent = '(' + this.files[0]?.name + ')' || "";
    currentBulkImportTemplate = {};
    document.getElementById('saveCSVImportTemplate').disabled = true;
    document.getElementById('deleteCSVImportTemplate').disabled = true;
    uploadCSVFileAndGetContents();
    
    enableDisableCSVUploadTabs('previewData', false);

});

// File Upload: Drag & Drop + Click -- End

// If this button is enabled, it means the user has selected an account and uploaded a valid CSV file. So, we can proceed to the next step.
document.getElementById('btnStep1Next').addEventListener('click', function (event) {

    event.preventDefault();

    const mapFields = document.getElementById('mapFields');  // Navigate to the next step (Map Fields)
    mapFields.disabled = false;
    mapFields.click();  // Trigger the click event to navigate to the next step
    displayCSVFileData();  // Display the CSV data as is..start from row 0 (header row) and let the user select the header row in the next step
    // getBulkImportTemplates();   // MUST BE CALLED AFTER displayCSVFileData();
    document.getElementById('btnStep2Next').disabled = false;
    // Disable the button...automatically enabled when there is a correct csv file...
    this.disabled = true;

});


document.getElementById('btnStep2Next').addEventListener('click', function (event) {

    event.preventDefault();

    if (isValidCSVFileData()) {  // Validate the CSV file data based on the selected column mappings

        const previewData = document.getElementById('previewData');  // Navigate to the next step (Map Fields)
        previewData.disabled = false;
        previewData.click();  // Trigger the click event to navigate to the next step
        displayCSVFileData(+currentBulkImportTemplate.headerRowIndex, true);
        // this.disabled = true;

    }  

});


// Configure Unobtrusive Validations Settings On Modal and it's Form (Must be last always)
setUpUnobtrusiveValidationOnModal('#csvFileUploadModal');