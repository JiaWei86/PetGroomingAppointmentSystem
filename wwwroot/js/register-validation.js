// Validation rules configuration
const validationRules = {
    phoneNumber: {
        pattern: /^01[0-9]-?[0-9]{7,8}$/,
        errorMessage: 'Phone must start with 01X and contain 8-9 digits (e.g., 012-3456789)',
        checkAvailability: true
    },
    name: {
        pattern: /^[a-zA-Z\s]{3,}$/,
        errorMessage: 'Name must contain only letters and spaces (minimum 3 characters)'
    },
    ic: {
        pattern: /^\d{6}-\d{2}-\d{4}$/,
        errorMessage: 'IC number must be in format xxxxxx-xx-xxxx (e.g., 130806-14-0728)'
    },
    email: {
        pattern: /^[^\s@]+@[^\s@]+\.[^\s@]+$/,
        errorMessage: 'Please enter a valid email address'
    },
    password: {
        minLength: 8,
        requireUppercase: true,
        requireLowercase: true,
        requireSymbol: true,
        errorMessage: 'Password must be at least 8 characters with 1 uppercase, 1 lowercase, and 1 symbol'
    },
    confirmPassword: {
        compareWith: 'password',
        errorMessage: 'Passwords do not match'
    }
};

const MalaysianStates = {
    '01': 'Johor', '02': 'Kedah', '03': 'Kelantan', '04': 'Malacca',
    '05': 'Negeri Sembilan', '06': 'Pahang', '07': 'Penang', '08': 'Perak',
    '09': 'Perlis', '10': 'Selangor', '11': 'Terengganu', '12': 'Sabah',
    '13': 'Sarawak', '14': 'Kuala Lumpur', '15': 'Labuan', '16': 'Putrajaya'
};

const DaysInMonth = [31, 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31];

function isLeapYear(year) {
    return (year % 4 === 0 && year % 100 !== 0) || (year % 400 === 0);
}

// Validate password strength
function validatePasswordStrength(password) {
    const rules = validationRules.password;
    return {
        length: password.length >= rules.minLength,
        uppercase: /[A-Z]/.test(password),
        lowercase: /[a-z]/.test(password),
        symbol: /[!@#$%^&*()_+\-=\[\]{};':"\\|,.<>\/?]/.test(password)
    };
}

// Update password requirement indicators
function updatePasswordRequirements(password) {
    const strength = validatePasswordStrength(password);
    
    document.getElementById('reqLength').classList.toggle('met', strength.length);
    document.getElementById('reqUppercase').classList.toggle('met', strength.uppercase);
    document.getElementById('reqLowercase').classList.toggle('met', strength.lowercase);
    document.getElementById('reqSymbol').classList.toggle('met', strength.symbol);
    
    return strength.length && strength.uppercase && strength.lowercase && strength.symbol;
}

// Malaysian IC validation - matches server-side logic exactly
function validateMalaysianIC(icNumber) {
    const icDigits = icNumber.replace(/-/g, '');

    // Check length
    if (icDigits.length !== 12) {
        return false;
    }

    const yearStr = icDigits.substring(0, 2);
    const monthStr = icDigits.substring(2, 4);
    const dayStr = icDigits.substring(4, 6);
    const stateStr = icDigits.substring(6, 8);

    const year = parseInt(yearStr, 10);
    const month = parseInt(monthStr, 10);
    const day = parseInt(dayStr, 10);

    // Validate year range
    if (isNaN(year) || year < 0 || year > 99) {
        return false;
    }

    // Convert 2-digit year to 4-digit year
    const currentYear = new Date().getFullYear();
    const currentYearStr = currentYear.toString();
    const currentYearLastTwo = parseInt(currentYearStr.substring(2), 10);
    const fullYear = year <= currentYearLastTwo ? 2000 + year : 1900 + year;

    // Check age (0-100 years)
    const age = currentYear - fullYear;
    if (age < 0 || age > 100) {
        return false;
    }

    // Validate month (1-12)
    if (isNaN(month) || month < 1 || month > 12) {
        return false;
    }

    // Validate day (1 or greater)
    if (isNaN(day) || day < 1) {
        return false;
    }

    // Get max days for month
    let maxDays = DaysInMonth[month - 1];
    if (month === 2 && isLeapYear(fullYear)) {
        maxDays = 29;
    }

    // CRITICAL: Check if day exceeds max days in month
    if (day > maxDays) {
        return false;
    }

    // Validate state code (01-16)
    if (!MalaysianStates.hasOwnProperty(stateStr)) {
        return false;
    }

    return true;
}

function validateFieldRealTime(fieldId, checkAvailability = false) {
    const field = document.getElementById(fieldId);
    if (!field) return null;

    const errorDiv = document.getElementById(fieldId + 'Error');
    const successDiv = document.getElementById(fieldId + 'Success');
    const icon = document.getElementById(fieldId + 'Icon');
    const rule = validationRules[fieldId];
    const formGroup = field.parentElement;

    // Empty field - clear everything
    if (!field.value || !field.value.trim()) {
        if (errorDiv) errorDiv.classList.remove('show');
        if (successDiv) successDiv.classList.remove('show');
        if (icon) icon.classList.remove('show');  // <-- This removes the icon
        if (formGroup) {
            formGroup.classList.remove('has-error');
            formGroup.classList.remove('has-success');
        }
        return null;
    }

    let isValid = false;
    let errorMsg = '';

    // Password confirmation check
    if (fieldId === 'confirmPassword') {
        const passwordField = document.getElementById('password');
        isValid = (field.value === passwordField.value) && (field.value.length >= 8);
        if (!isValid) {
            errorMsg = rule.errorMessage;
        }
    }
    // Password strength check
    else if (fieldId === 'password') {
        const strength = validatePasswordStrength(field.value);
        isValid = strength.length && strength.uppercase && strength.lowercase && strength.symbol;
        if (!isValid) {
            errorMsg = rule.errorMessage;
        }
    }
    // IC field special handling
    else if (fieldId === 'ic') {
        // First: Check format using regex
        const formatValid = rule.pattern.test(field.value);

        if (!formatValid) {
            // Format is invalid
            isValid = false;
            errorMsg = rule.errorMessage;
        } else {
            // Format is valid, now check IC date/state validity
            isValid = validateMalaysianIC(field.value);
            if (!isValid) {
                errorMsg = 'IC number is invalid. Please check the birth date (YYMMDD) and state code (01-16).';
            }
        }
    }
    // Standard pattern validation
    else if (rule && rule.pattern) {
        isValid = rule.pattern.test(field.value);
        if (!isValid) {
            errorMsg = rule.errorMessage;
        }
    }

    // Update UI: Show error or success
    if (!isValid) {
        if (!errorMsg) {
            errorMsg = (rule && rule.errorMessage) ? rule.errorMessage : 'Invalid input';
        }

        // Show error
        if (errorDiv) {
            errorDiv.textContent = errorMsg;
            errorDiv.classList.add('show');
        }
        if (successDiv) {
            successDiv.classList.remove('show');
        }
        if (icon) {
            icon.classList.remove('success');
            icon.classList.add('show', 'error');
            icon.textContent = '✕';
        }
        if (formGroup) {
            formGroup.classList.add('has-error');
            formGroup.classList.remove('has-success');
        }
        return false;
    } else {
        // Phone availability check
        if (fieldId === 'phoneNumber' && checkAvailability) {
            checkPhoneAvailability(field.value, errorDiv, successDiv, icon, formGroup);
            return null;
        }

        // Show success
        if (errorDiv) {
            errorDiv.classList.remove('show');
        }
        if (successDiv) {
            successDiv.classList.add('show');
            successDiv.textContent = '[OK] Valid ' + fieldId.replace(/([A-Z])/g, ' $1').toLowerCase();
        }
        if (icon) {
            icon.classList.add('show', 'success');
            icon.classList.remove('error');
            icon.textContent = '✓';
        }
        if (formGroup) {
            formGroup.classList.add('has-success');
            formGroup.classList.remove('has-error');
        }
        return true;
    }
}

function checkPhoneAvailability(phoneNumber, errorDiv, successDiv, icon, formGroup) {
    clearTimeout(window.phoneCheckTimeout);

    window.phoneCheckTimeout = setTimeout(async () => {
        try {
            const phoneWithoutDash = phoneNumber.replace(/-/g, '');

            const response = await fetch('/Customer/Auth/CheckPhoneNumber', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'X-Requested-With': 'XMLHttpRequest'
                },
                body: JSON.stringify({ phoneNumber: phoneWithoutDash })
            });

            if (!response.ok) throw new Error('Network response error');

            const data = await response.json();

            if (data.available) {
                errorDiv.classList.remove('show');
                successDiv.classList.add('show');
                icon.classList.add('show', 'success');
                icon.textContent = '✓';
                formGroup.classList.add('has-success');
                formGroup.classList.remove('has-error');
            } else {
                errorDiv.textContent = 'Phone number already registered.';
                errorDiv.classList.add('show');
                successDiv.classList.remove('show');
                icon.classList.add('show', 'error');
                icon.textContent = '✕';
                formGroup.classList.add('has-error');
                formGroup.classList.remove('has-success');
            }
        } catch (error) {
            console.error('Phone availability check error:', error);
        }
    }, 500);
}

// Clear validation on page load
document.addEventListener('DOMContentLoaded', function () {
    ['phoneNumber', 'name', 'ic', 'email', 'password', 'confirmPassword'].forEach(fieldId => {
        const icon = document.getElementById(fieldId + 'Icon');
        const errorDiv = document.getElementById(fieldId + 'Error');
        const successDiv = document.getElementById(fieldId + 'Success');
        const field = document.getElementById(fieldId);

        if (icon) icon.classList.remove('show');
        if (errorDiv) errorDiv.classList.remove('show');
        if (successDiv) successDiv.classList.remove('show');
        if (field && field.parentElement) {
            field.parentElement.classList.remove('has-error', 'has-success');
        }
    });
});

// Password visibility toggle
document.addEventListener('DOMContentLoaded', function() {
    const passwordToggle = document.getElementById('passwordToggle');
    const passwordInput = document.getElementById('password');
    
    const confirmPasswordToggle = document.getElementById('confirmPasswordToggle');
    const confirmPasswordInput = document.getElementById('confirmPassword');
    
    if (passwordToggle && passwordInput) {
        passwordToggle.addEventListener('click', function(e) {
            e.preventDefault();
            const isPassword = passwordInput.type === 'password';
            passwordInput.type = isPassword ? 'text' : 'password';
            
            const icon = this.querySelector('i');
            if (isPassword) {
                // Password is being shown, change to eye-slash (hidden state)
                icon.classList.remove('bi-eye-slash');
                icon.classList.add('bi-eye');
            } else {
                // Password is being hidden, change to eye (visible state)
                icon.classList.remove('bi-eye');
                icon.classList.add('bi-eye-slash');
            }
        });
    }
    
    if (confirmPasswordToggle && confirmPasswordInput) {
        confirmPasswordToggle.addEventListener('click', function(e) {
            e.preventDefault();
            const isPassword = confirmPasswordInput.type === 'password';
            confirmPasswordInput.type = isPassword ? 'text' : 'password';
            
            const icon = this.querySelector('i');
            if (isPassword) {
                // Password is being shown, change to eye-slash (hidden state)
                icon.classList.remove('bi-eye-slash');
                icon.classList.add('bi-eye');
            } else {
                // Password is being hidden, change to eye (visible state)
                icon.classList.remove('bi-eye');
                icon.classList.add('bi-eye-slash');
            }
        });
    }
    
    // Real-time password strength validation
    if (passwordInput) {
        passwordInput.addEventListener('input', function() {
            updatePasswordRequirements(this.value);
        });
    }
});

// Input event listeners
document.getElementById('phoneNumber')?.addEventListener('input', function (e) {
    let value = e.target.value.replace(/\D/g, '');
    if (value.length > 0) {
        if (value.length <= 3) {
            e.target.value = value;
        } else if (value.length <= 10) {
            e.target.value = value.slice(0, 3) + '-' + value.slice(3);
        } else {
            e.target.value = value.slice(0, 3) + '-' + value.slice(3, 11);
        }
    }
    validateFieldRealTime('phoneNumber', true);
});

document.getElementById('ic')?.addEventListener('input', function (e) {
    let value = e.target.value.replace(/\D/g, '');
    if (value.length > 0) {
        if (value.length <= 6) {
            e.target.value = value;
        } else if (value.length <= 8) {
            e.target.value = value.slice(0, 6) + '-' + value.slice(6);
        } else {
            e.target.value = value.slice(0, 6) + '-' + value.slice(6, 8) + '-' + value.slice(8, 12);
        }
    }
    validateFieldRealTime('ic');
});

document.getElementById('ic')?.addEventListener('blur', function () {
    validateFieldRealTime('ic');
});

['name', 'email', 'password'].forEach(fieldId => {
    document.getElementById(fieldId)?.addEventListener('input', function () {
        validateFieldRealTime(fieldId);
    });
});

document.getElementById('confirmPassword')?.addEventListener('input', function () {
    validateFieldRealTime('confirmPassword');
    if (document.getElementById('password').value) {
        validateFieldRealTime('password');
    }
});

// Form submission
document.getElementById('registerForm')?.addEventListener('submit', async (e) => {
    e.preventDefault();

    const fields = ['phoneNumber', 'name', 'ic', 'email', 'password', 'confirmPassword'];
    let allValid = true;

    fields.forEach(fieldId => {
        const errorDiv = document.getElementById(fieldId + 'Error');
        if (errorDiv) errorDiv.classList.remove('show');
    });

    fields.forEach(fieldId => {
        const field = document.getElementById(fieldId);
        if (!field.value.trim()) {
            allValid = false;
        } else {
            const result = validateFieldRealTime(fieldId);
            if (!result) {
                allValid = false;
            }
        }
    });

    if (!allValid) {
        const alertContainer = document.getElementById('alertContainer');
        alertContainer.innerHTML = '<div class="alert alert-danger">Please fix all errors before submitting</div>';
        return;
    }

    const submitBtn = document.getElementById('submitBtn');
    submitBtn.disabled = true;
    submitBtn.innerHTML = '<span class="spinner"></span>Registering...';

    try {
        const formData = new FormData(document.getElementById('registerForm'));
        const response = await fetch(document.getElementById('registerForm').getAttribute('action') || '/Customer/Auth/Register', {
            method: 'POST',
            body: formData,
            headers: { 'X-Requested-With': 'XMLHttpRequest' }
        });

        const data = await response.json();
        const alertContainer = document.getElementById('alertContainer');

        if (data.success) {
            alertContainer.innerHTML = '<div class="alert alert-success">' + data.message + '</div>';
            document.getElementById('registerForm').reset();
            setTimeout(() => {
                window.location.href = '/Customer/Auth/Login';
            }, 1000);
        } else {
            alertContainer.innerHTML = '<div class="alert alert-danger">' + data.message + '</div>';

            if (data.errors) {
                const fieldMap = {
                    'PhoneNumber': 'phoneNumber',
                    'Name': 'name',
                    'IC': 'ic',
                    'Email': 'email',
                    'Password': 'password',
                    'ConfirmPassword': 'confirmPassword'
                };

                for (const [key, value] of Object.entries(data.errors)) {
                    const fieldId = fieldMap[key] || key.toLowerCase();
                    const errorDiv = document.getElementById(fieldId + 'Error');
                    const field = document.getElementById(fieldId);

                    if (errorDiv) {
                        errorDiv.textContent = value;
                        errorDiv.classList.add('show');
                    }
                    if (field) {
                        field.parentElement.classList.add('has-error');
                        field.parentElement.classList.remove('has-success');
                    }
                }
            }
        }
    } catch (error) {
        console.error('Form submission error:', error);
        document.getElementById('alertContainer').innerHTML = '<div class="alert alert-danger">An error occurred during registration</div>';
    } finally {
        submitBtn.disabled = false;
        submitBtn.innerHTML = 'Register';
    }
});