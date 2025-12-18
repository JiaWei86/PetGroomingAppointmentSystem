console.log('groomer.js loaded');

// State holders for confirm modal actions
let currentAddForm = null;
let currentEditForm = null;
let currentDeleteForm = null;

// GROOMER AJAX FORM SUBMISSION & VALIDATION
// Moved from Groomer.cshtml to avoid Razor parsing issues

// Debounce helper for live validation
function debounce(fn, wait) {
 let t;
 return function(...args) {
 const ctx = this;
 clearTimeout(t);
 t = setTimeout(() => fn.apply(ctx, args), wait);
 };
}

document.addEventListener('DOMContentLoaded', function() {
 const groomerForm = document.getElementById('addGroomerForm');
 if (groomerForm) {
 groomerForm.addEventListener('submit', handleGroomerFormSubmit);
 }
 initializeGroomerFieldValidation();

 // ? Bind Confirm button click
 const confirmBtn = document.getElementById('confirmBtn');
 if (confirmBtn) {
 confirmBtn.addEventListener('click', function () {
 console.log('confirmBtn clicked');
 if (currentDeleteForm) {
 currentDeleteForm.submit();
 } else if (currentEditForm) {
 const userId = currentEditForm.querySelector('input[name="editStaffId"]').value;
 if (typeof handleEditGroomerSubmit === 'function') {
 handleEditGroomerSubmit(userId);
 }
 closeConfirmModal();
 } else if (currentAddForm) {
 console.log('submitting add form');
 currentAddForm.submit();
 }
 });
 }
});

function handleGroomerFormSubmit(event) {
 event.preventDefault();
 clearGroomerErrors();

 const form = event.target;
 const formData = new FormData(form);
 const submitBtn = document.getElementById('submitGroomerBtn');

 submitBtn.disabled = true;
 submitBtn.innerHTML = '<i class="material-icons">hourglass_empty</i> Creating...';

 const tokenInput = form.querySelector('input[name="__RequestVerificationToken"]');
 const token = tokenInput ? tokenInput.value : '';

 fetch('/Admin/Home/CreateGroomerAjax', {
 method: 'POST',
 headers: { 'RequestVerificationToken': token },
 body: formData
 })
 .then(response => {
 if (!response.ok) {
 throw new Error(`HTTP error! status: ${response.status}`);
 }
 return response.json();
 })
 .then(data => {
 if (data.success) {
 showGlobalSuccessMessage(data.message || 'Groomer created successfully!');
 form.reset();
 hideCreateForm();
 setTimeout(() => window.location.reload(),1500);
 } else {
 displayGroomerErrors(data.errors);
 }
 })
 .catch(error => {
 console.error('Error:', error);
 showGlobalErrorMessage('Failed to create groomer. Please try again.');
 })
 .finally(() => {
 submitBtn.disabled = false;
 submitBtn.innerHTML = '<i class="material-icons">add</i> Save Groomer';
 });
}

function displayGroomerErrors(errors) {
 const errorContainer = document.getElementById('ajaxErrorContainer');
 const errorList = document.getElementById('ajaxErrorList');

 if (!errors || Object.keys(errors).length ===0) return;

 errorList.innerHTML = '';

 Object.keys(errors).forEach(field => {
 const errorMessage = errors[field];

 const li = document.createElement('li');
 li.textContent = errorMessage;
 errorList.appendChild(li);

 const errorSpan = document.getElementById(`error-${field}`);
 if (errorSpan) {
 setFieldInvalid(errorSpan, document.getElementById(getElementIdForField(field)), errorMessage);
 }

 const input = document.getElementById(getElementIdForField(field));
 if (input) {
 input.style.borderColor = '#dc3545';
 }
 });

 if (errorContainer) {
 errorContainer.style.display = 'block';
 errorContainer.scrollIntoView({ behavior: 'smooth', block: 'center' });
 }
}

function clearGroomerErrors() {
 const errorContainer = document.getElementById('ajaxErrorContainer');
 const errorList = document.getElementById('ajaxErrorList');

 if (errorContainer) errorContainer.style.display = 'none';
 if (errorList) errorList.innerHTML = '';

 document.querySelectorAll('.error-message').forEach(span => {
 span.textContent = '';
 span.style.display = 'none';
 span.style.color = '';
 });

 document.querySelectorAll('#addGroomerForm input, #addGroomerForm textarea, #addGroomerForm select').forEach(input => {
 input.style.borderColor = '';
 });
}

function showGlobalSuccessMessage(message) {
 const alertDiv = document.createElement('div');
 alertDiv.className = 'alert alert-success';
 alertDiv.id = 'tempSuccessAlert';
 alertDiv.innerHTML = `<strong> Success:</strong> ${message}`;
 alertDiv.style.cssText = `position: fixed; top:85px; left: calc(var(--sidebar-width) +35px); right:35px; z-index:9999; padding:24px32px; font-size:18px; font-weight:600; border-radius:12px; background: linear-gradient(135deg, #d1fae50%, #a7f3d0100%); color: #065f46; border-left:6px solid #10b981; box-shadow:08px20px rgba(0,0,0,0.25); animation: slideDown0.5s ease;`;

 document.body.appendChild(alertDiv);

 setTimeout(() => {
 alertDiv.style.transition = 'opacity0.5s ease';
 alertDiv.style.opacity = '0';
 setTimeout(() => alertDiv.remove(),500);
 },5000);
}

function showGlobalErrorMessage(message) {
 const alertDiv = document.createElement('div');
 alertDiv.className = 'alert alert-danger';
 alertDiv.id = 'tempErrorAlert';
 alertDiv.innerHTML = `<strong>? Error:</strong> ${message}`;
 alertDiv.style.cssText = `position: fixed; top:85px; left: calc(var(--sidebar-width) +35px); right:35px; z-index:9999; padding:24px32px; font-size:18px; font-weight:600; border-radius:12px; background: linear-gradient(135deg, #fee2e20%, #fecaca100%); color: #991b1b; border-left:6px solid #ef4444; box-shadow:08px20px rgba(0,0,0,0.25); animation: slideDown0.5s ease;`;

 document.body.appendChild(alertDiv);

 setTimeout(() => {
 alertDiv.style.transition = 'opacity0.5s ease';
 alertDiv.style.opacity = '0';
 setTimeout(() => alertDiv.remove(),500);
 },5000);
}

const originalHideCreateForm = window.hideCreateForm;
window.hideCreateForm = function() {
 clearGroomerErrors();
 if (originalHideCreateForm) {
 originalHideCreateForm();
 } else {
 const el = document.getElementById('createGroomerForm');
 if (el) el.style.display = 'none';
 }
};

function initializeGroomerFieldValidation() {
 const nameInput = document.getElementById('groomerName');
 const icInput = document.getElementById('groomerIC');
 const emailInput = document.getElementById('groomerEmail');
 const phoneInput = document.getElementById('groomerPhone');
 const experienceInput = document.getElementById('experienceYear');
 const positionInput = document.getElementById('position');
 const descriptionInput = document.getElementById('description');
 const photoInput = document.getElementById('photoUpload');

 if (nameInput) {
 nameInput.addEventListener('blur', function() { validateName(this.value); });
 nameInput.addEventListener('input', debounce(function() { validateName(this.value); }, 200));
 }

 if (icInput) {
 icInput.addEventListener('input', function() { formatICNumber(this); });
 icInput.addEventListener('input', debounce(function() { validateIC(this.value); },200));
 icInput.addEventListener('blur', function() { validateIC(this.value); });
 }

 if (emailInput) {
 emailInput.addEventListener('blur', function() { validateEmail(this.value); });
 emailInput.addEventListener('input', debounce(function() { validateEmail(this.value); },200));
 emailInput.addEventListener('input', function() { if (this.value.length >0) clearFieldError('groomerEmail','error-Email'); });
 }

 if (phoneInput) {
 phoneInput.addEventListener('input', function() { formatPhoneNumber(this); });
 phoneInput.addEventListener('input', debounce(function() { validatePhone(this.value); },200));
 phoneInput.addEventListener('blur', function() { validatePhone(this.value); });
 }

 if (experienceInput) {
 experienceInput.addEventListener('blur', function() { validateExperience(this.value); });
 experienceInput.addEventListener('input', debounce(function() { validateExperience(this.value) }, 200));
 }

 if (positionInput) {
 positionInput.addEventListener('blur', function() { validatePosition(this.value); });
 positionInput.addEventListener('change', function() { validatePosition(this.value); });
 }

 if (descriptionInput) {
 descriptionInput.addEventListener('blur', function() { validateDescription(this.value); });
 }

 if (photoInput) {
 photoInput.addEventListener('change', function() { /* preview omitted */ });
 }
}

// ====== Helper UI functions ======
function getElementIdForField(field) {
 // mapping from server field names to input ids
 const map = { 'Name':'groomerName', 'IC':'groomerIC', 'Email':'groomerEmail', 'Phone':'groomerPhone', 'ExperienceYear':'experienceYear', 'Position':'position', 'Description':'description' };
 return map[field] || field.toLowerCase();
}

function setFieldInvalid(errorSpan, input, message) {
 if (errorSpan) {
 errorSpan.textContent = message;
 errorSpan.style.display = 'block';
 errorSpan.style.color = '#ef4444';
 }
 if (input) input.style.borderColor = '#ef4444';
}

function setFieldValid(errorSpan, input, message) {
 if (errorSpan) {
 errorSpan.textContent = message || 'Valid';
 errorSpan.style.display = 'block';
 errorSpan.style.fontWeight = 'bold';
 errorSpan.style.setProperty('color', '#28a745', 'important');
 }
 if (input) input.style.borderColor = '#28a745';
}

function clearFieldError(inputId, errorId) {
 const input = document.getElementById(inputId);
 const errorSpan = document.getElementById(errorId);
 if (input) input.style.borderColor = '';
 if (errorSpan) {
 errorSpan.textContent = '';
 errorSpan.style.display = 'none';
 errorSpan.style.color = '';
 }
}

// ====== Validation functions (client + optional server AJAX) ======
async function validateName(value) {
    const errorSpan = document.getElementById('error-Name');
    const input = document.getElementById('groomerName');
    if (!input) return true;

    // Clear previous error before new validation
    clearFieldError('groomerName', 'error-Name');

    if (!value || value.trim().length < 2) {
        setFieldInvalid(errorSpan, input, 'Name must be at least 2 characters long.');
        return false;
    }

    // Server-side validation for characters
    try {
        const resp = await fetch('/Admin/Home/ValidateGroomerField', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                FieldName: 'Name',
                FieldValue: value,
                StaffId: '' // 'add' mode
            })
        });
        const data = await resp.json();
        if (data.isValid) {
            setFieldValid(errorSpan, input, 'Valid');
            return true;
        } else {
            setFieldInvalid(errorSpan, input, data.errorMessage || 'Invalid characters in name.');
            return false;
        }
    } catch (e) {
        console.error('Name validation ajax error', e);
        // Fallback to client-side valid message if server check fails
        setFieldValid(errorSpan, input, 'Valid (local)');
        return true; // Don't block submission if server is down
    }
}


function validateAgeVsExperience() {
    const icInput = document.getElementById('groomerIC');
    const experienceInput = document.getElementById('experienceYear');
    const errorSpan = document.getElementById('error-ExperienceYear');

    if (!icInput || !experienceInput || !errorSpan) return true; // Can't validate

    const icValue = icInput.value;
    const experienceValue = experienceInput.value;

    if (!icValue || !experienceValue) return true; // Not enough info

    const icRegex = /^\d{6}-\d{2}-\d{4}$/;
    if (!icRegex.test(icValue)) return true; // IC not valid yet

    const experience = parseInt(experienceValue, 10);
    if (isNaN(experience) || experience < 0) return true; // Experience not valid yet

    try {
        const rawIC = icValue.replace(/-/g, '');
        const yy = parseInt(rawIC.substr(0, 2), 10);
        const currentCentury = Math.floor(new Date().getFullYear() / 100) * 100; // e.g., 2000
        const currentYY = new Date().getFullYear() % 100; // e.g., 25 for 2025
        
        // If YY is greater than current YY, it's likely previous century
        const year = (yy > currentYY) ? (currentCentury - 100 + yy) : (currentCentury + yy);

        const mm = parseInt(rawIC.substr(2, 2), 10);
        const dd = parseInt(rawIC.substr(4, 2), 10);
        
        const birthDate = new Date(year, mm - 1, dd);
        if (isNaN(birthDate.getTime()) || birthDate.getFullYear() !== year || birthDate.getMonth() + 1 !== mm || birthDate.getDate() !== dd) {
             return true; // Invalid date components
        }

        let age = new Date().getFullYear() - birthDate.getFullYear();
        const m = new Date().getMonth() - birthDate.getMonth();
        if (m < 0 || (m === 0 && new Date().getDate() < birthDate.getDate())) {
            age--;
        }

        // A person can start gaining professional experience around age 16.
        if (experience > (age - 16)) {
            setFieldInvalid(errorSpan, experienceInput, `Experience of ${experience} years is unrealistic for someone aged ${age}.`);
            return false;
        }
        return true;
    } catch {
        return true; // Don't block if parsing fails
    }
}


async function validateIC(value) {
 const errorSpan = document.getElementById('error-IC');
 const input = document.getElementById('groomerIC');
 if (!input) return true;

 // Format check
 const icRegex = /^\d{6}-\d{2}-\d{4}$/;
 if (!value || !icRegex.test(value)) {
 setFieldInvalid(errorSpan, input, 'IC must be in format xxxxxx-xx-xxxx.');
 return false;
 }

 try {
 const raw = value.replace(/-/g, '');
 const yy = parseInt(raw.substr(0,2),10);
 const mm = parseInt(raw.substr(2,2),10);
 const dd = parseInt(raw.substr(4,2),10);
 const today = new Date();

        const currentCentury = Math.floor(new Date().getFullYear() / 100) * 100; // e.g., 2000
        const currentYY = new Date().getFullYear() % 100; // e.g., 25 for 2025
        
        // If YY is greater than current YY, it's likely previous century
        const year = (yy > currentYY) ? (currentCentury - 100 + yy) : (currentCentury + yy);
 
 const birth = new Date(year, mm -1, dd);
 if (isNaN(birth.getTime()) || birth.getFullYear() !== year || birth.getMonth() + 1 !== mm || birth.getDate() !== dd) {
 setFieldInvalid(errorSpan, input, 'Invalid birth date inside IC.');
 return false;
 }
 if (birth > today) {
 setFieldInvalid(errorSpan, input, 'Birth date cannot be in the future.');
 return false;
 }
 // compute age
 let age = today.getFullYear() - birth.getFullYear();
 const m = today.getMonth() - birth.getMonth();
 if (m <0 || (m ===0 && today.getDate() < birth.getDate())) age--;

 if (age <18 || age >60) {
 setFieldInvalid(errorSpan, input, `Working age must be between 18 and 60. Current age: ${age}.`);
 return false;
 }
 
  // After validating age, check against experience
  validateAgeVsExperience();


 // server-side uniqueness check
 try {
      const resp = await fetch('/Admin/Home/ValidateGroomerField', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({
              FieldName: 'IC',
              FieldValue: value,
              StaffId: '' // Pass empty StaffId for 'add' mode
          })
      });
 const data = await resp.json();
      if (data.isValid) {
 setFieldValid(errorSpan, input, `Valid (Age: ${age})`);
 return true;
 } else {
          setFieldInvalid(errorSpan, input, data.errorMessage || 'IC validation failed.');
 return false;
 }
 } catch (e) {
 console.error('IC validation ajax error', e);
 setFieldInvalid(errorSpan, input, 'Validation check failed.');
 return false;
 }

 } catch (ex) {
 console.error('IC parse error', ex);
 setFieldInvalid(errorSpan, input, 'Invalid IC value.');
 return false;
 }
}

async function validateEmail(value) {
 const errorSpan = document.getElementById('error-Email');
 const input = document.getElementById('groomerEmail');
 if (!input) return true;

 const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
 if (!value || !emailRegex.test(value)) {
 setFieldInvalid(errorSpan, input, 'Please enter a valid email address.');
 return false;
 }

 // server-side uniqueness
 try {
      const resp = await fetch('/Admin/Home/ValidateGroomerField', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({
              FieldName: 'Email',
              FieldValue: value,
              StaffId: '' // Pass empty StaffId for 'add' mode
          })
      });
 const data = await resp.json();
      if (data.isValid) {
 setFieldValid(errorSpan, input, 'Valid');
 return true;
 } else {
          setFieldInvalid(errorSpan, input, data.errorMessage || 'Email validation failed.');
 return false;
 }
 } catch (e) {
 console.error('Email validation ajax error', e);
 setFieldInvalid(errorSpan, input, 'Validation check failed.');
 return false;
 }
}

async function validatePhone(value) {
 const errorSpan = document.getElementById('error-Phone');
 const input = document.getElementById('groomerPhone');
 if (!input) return true;

 const phoneRegex = /^01[0-9]-[0-9]{7,8}$/;
 if (!value || !phoneRegex.test(value)) {
 setFieldInvalid(errorSpan, input, 'Phone must be in format 01X-XXXXXXX or 01X-XXXXXXXX.');
 return false;
 }

 // server-side uniqueness
 try {
      const resp = await fetch('/Admin/Home/ValidateGroomerField', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({
              FieldName: 'Phone',
              FieldValue: value,
              StaffId: '' // Pass empty StaffId for 'add' mode
          })
      });
 const data = await resp.json();
      if (data.isValid) {
 setFieldValid(errorSpan, input, 'Valid');
 return true;
 } else {
          setFieldInvalid(errorSpan, input, data.errorMessage || 'Phone validation failed.');
 return false;
 }
 } catch (e) {
 console.error('Phone validation ajax error', e);
 setFieldInvalid(errorSpan, input, 'Validation check failed.');
 return false;
 }
}

function validateExperience(value) {
    const errorSpan = document.getElementById('error-ExperienceYear');
    const input = document.getElementById('experienceYear');
    if (!input) return true;

    // This field is not required, so if it's empty, it's valid.
    if (value === '' || value === null) {
        clearFieldError('experienceYear', 'error-ExperienceYear');
        return true;
    }
    
    const num = parseInt(value, 10);

    if (isNaN(num)) {
        setFieldInvalid(errorSpan, input, 'Experience must be a number.');
        return false;
    }
    if (num < 0) {
        setFieldInvalid(errorSpan, input, 'Experience cannot be negative.');
        return false;
    }
    if (num > 50) {
        setFieldInvalid(errorSpan, input, 'Experience must be between 0 and 50 years.');
        return false;
    }

    // Run the cross-validation with age
    if (!validateAgeVsExperience()) {
        return false; // Error is already set by the helper
    }
    
    setFieldValid(errorSpan, input, 'Valid');
    return true;
}


function validatePosition(value) {
 const errorSpan = document.getElementById('error-Position');
 const input = document.getElementById('position');
 if (!input) return true;
 const validPositions = ['Senior Groomer', 'Junior Groomer', 'Groomer Assistant'];
 if (!value || !validPositions.includes(value)) {
 setFieldInvalid(errorSpan, input, 'Please select a valid position.');
 return false;
 } else {
 setFieldValid(errorSpan, input, 'Valid');
 return true;
 }
}

function validateDescription(value) {
 const errorSpan = document.getElementById('error-Description');
 const input = document.getElementById('description');
 if (!input) return true;
 if (value && value.length >500) {
 setFieldInvalid(errorSpan, input, 'Description must not exceed500 characters.');
 return false;
 } else {
 setFieldValid(errorSpan, input, 'Valid');
 return true;
 }
}

// formatting helpers
function formatICNumber(input) {
  const originalValue = input.value;
  const originalCursorPos = input.selectionStart;

  // Count dashes before cursor in original value
  const dashesBeforeCursorOld = (originalValue.substring(0, originalCursorPos).match(/-/g) || []).length;

  // Get only digits from the input
  let digits = originalValue.replace(/\D/g, '');

  // Truncate to max length of 12 digits
  if (digits.length > 12) {
    digits = digits.substring(0, 12);
  }

  // Apply formatting: xxxxxx-xx-xxxx
  let formattedValue = digits;
  if (digits.length > 6) {
    formattedValue = `${digits.substring(0, 6)}-${digits.substring(6, 8)}`;
    if (digits.length > 8) {
      formattedValue += `-${digits.substring(8)}`;
    }
  }

  // Count dashes before cursor in new value
  const dashesBeforeCursorNew = (formattedValue.substring(0, originalCursorPos).match(/-/g) || []).length;

  // Calculate new cursor position
  const newCursorPos = originalCursorPos + (dashesBeforeCursorNew - dashesBeforeCursorOld);

  // Set the new value and cursor position
  input.value = formattedValue;
  input.setSelectionRange(newCursorPos, newCursorPos);
}

function formatPhoneNumber(input) {
 let value = input.value.replace(/\D/g, '');
 if (value.length >3) value = value.slice(0,3) + '-' + value.slice(3);
 if (value.length >12) value = value.slice(0,12);
 input.value = value;
}

// Photo upload handler (keeps existing behavior expected by markup)
if (typeof handlePhotoUpload === 'undefined') {
 window.handlePhotoUpload = function(input, userId) {


 if (!statusSpan) return;

 if (input.files && input.files.length >0) {
 const fileName = input.files[0].name;
 const fileSize = (input.files[0].size /1024 /1024).toFixed(2);

 statusSpan.style.display = 'block';

 if (input.files[0].size >5 *1024 *1024) {
 statusSpan.innerHTML = `? File too large: ${fileSize}MB (Max5MB)`;
 statusSpan.style.color = '#ef4444';
 statusSpan.style.fontWeight = '600';
 input.value = '';
 } else {
 statusSpan.innerHTML = `? Selected: ${fileName} (${fileSize}MB)`;
 statusSpan.style.color = '#10b981';
 statusSpan.style.fontWeight = '600';
 }
 } else {
 statusSpan.style.display = 'none';
 }
 };
}

// --- Add Confirm ---
function openConfirmAdd() {
 console.log('openConfirmAdd called');
 currentAddForm = document.getElementById('addGroomerForm');

 if (!currentAddForm) {
 console.warn('Add form not found');
 return;
 }

 const modal = document.getElementById('confirmModal');
 const title = document.getElementById('confirmTitle');
 const message = document.getElementById('confirmMessage');
 const icon = document.getElementById('confirmIcon');

 if (title) title.textContent = 'Confirm Add';
 if (message) message.textContent = 'Are you sure you want to add this new groomer?';
 if (icon) icon.textContent = 'person_add';

 modal?.classList.add('show');
 console.log('modal shown');
}
