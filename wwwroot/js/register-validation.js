// Validation rules configuration
const validationRules = {
    phoneNumber: {
        pattern: /^01[0-9]-[0-9]{7,8}$/,
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

    const reqLength = document.getElementById('reqLength');
    const reqUppercase = document.getElementById('reqUppercase');
    const reqLowercase = document.getElementById('reqLowercase');
    const reqSymbol = document.getElementById('reqSymbol');

    if (reqLength) reqLength.classList.toggle('met', strength.length);
    if (reqUppercase) reqUppercase.classList.toggle('met', strength.uppercase);
    if (reqLowercase) reqLowercase.classList.toggle('met', strength.lowercase);
    if (reqSymbol) reqSymbol.classList.toggle('met', strength.symbol);

    return strength.length && strength.uppercase && strength.lowercase && strength.symbol;
}

// Malaysian IC validation
function validateMalaysianIC(icNumber) {
    const icDigits = icNumber.replace(/-/g, '');

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

    if (isNaN(year) || year < 0 || year > 99) {
        return false;
    }

    const currentYear = new Date().getFullYear();
    const currentYearStr = currentYear.toString();
    const currentYearLastTwo = parseInt(currentYearStr.substring(2), 10);
    const fullYear = year <= currentYearLastTwo ? 2000 + year : 1900 + year;

    const age = currentYear - fullYear;
    if (age < 0 || age > 100) {
        return false;
    }

    if (isNaN(month) || month < 1 || month > 12) {
        return false;
    }

    if (isNaN(day) || day < 1) {
        return false;
    }

    let maxDays = DaysInMonth[month - 1];
    if (month === 2 && isLeapYear(fullYear)) {
        maxDays = 29;
    }

    if (day > maxDays) {
        return false;
    }

    if (!MalaysianStates.hasOwnProperty(stateStr)) {
        return false;
    }

    return true;
}

// Debounce helper
function debounce(fn, wait) {
    let t;
    return function (...args) {
        const ctx = this;
        clearTimeout(t);
        t = setTimeout(() => fn.apply(ctx, args), wait);
    };
}

// Check phone availability via AJAX (SINGLE FUNCTION - NO DUPLICATES)
async function checkPhoneAvailability() {
    const phoneInput = document.getElementById('phoneNumber');
    if (!phoneInput) return;

    const errorDiv = document.getElementById('phoneNumberError');
    const successDiv = document.getElementById('phoneNumberSuccess');
    const icon = document.getElementById('phoneNumberIcon');
    const formGroup = phoneInput.parentElement;

    const phoneValue = phoneInput.value.trim();

    // Empty field check
    if (!phoneValue) {
        if (errorDiv) errorDiv.classList.remove('show');
        if (successDiv) successDiv.classList.remove('show');
        if (icon) icon.classList.remove('show');
        if (formGroup) {
            formGroup.classList.remove('has-error', 'has-success');
        }
        return;
    }

    // Format check first
    const phoneRegex = /^01[0-9]-[0-9]{7,8}$/;
    if (!phoneRegex.test(phoneValue)) {
        console.log(`[Phone] Format invalid: ${phoneValue}`);
        if (errorDiv) {
            errorDiv.textContent = 'Phone must be in format 01X-XXXXXXX or 01X-XXXXXXXX.';
            errorDiv.classList.add('show');
        }
        if (successDiv) successDiv.classList.remove('show');
        if (icon) {
            icon.classList.add('show', 'error');
            icon.classList.remove('success');
            icon.textContent = '✕';
        }
        if (formGroup) {
            formGroup.classList.add('has-error');
            formGroup.classList.remove('has-success');
        }
        return;
    }

    // Show loading state
    if (icon) {
        icon.classList.add('show');
        icon.textContent = '⏳';
        icon.classList.remove('success', 'error');
    }
    if (errorDiv) errorDiv.classList.remove('show');
    if (successDiv) successDiv.classList.remove('show');

    try {
        // Send WITHOUT dashes
        const phoneWithoutDash = phoneValue.replace(/-/g, '');
        console.log(`[Phone] Checking availability for: ${phoneWithoutDash}`);

        const response = await fetch('/Customer/Auth/CheckPhoneNumber', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'X-Requested-With': 'XMLHttpRequest'
            },
            body: JSON.stringify({ phoneNumber: phoneWithoutDash })
        });

        if (!response.ok) {
            throw new Error(`HTTP ${response.status}`);
        }

        const data = await response.json();
        console.log(`[Phone] Server response:`, data);

        // CRITICAL: Handle response - ALWAYS clear BOTH messages first, then show ONE
        if (data.available === true) {
            console.log(`[Phone] ✓ AVAILABLE - Phone is free to register`);

            // MUST clear error first
            if (errorDiv) {
                errorDiv.textContent = '';
                errorDiv.classList.remove('show');
            }
            
            // Show success
            if (successDiv) {
                successDiv.textContent = '[OK] Valid phone number';
                successDiv.classList.add('show');
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
        } else if (data.available === false) {
            console.log(`[Phone] ✗ NOT AVAILABLE - Phone already registered`);

            // MUST clear success first
            if (successDiv) {
                successDiv.textContent = '';
                successDiv.classList.remove('show');
            }
            
            // Show error
            if (errorDiv) {
                errorDiv.textContent = 'Phone number already registered.';
                errorDiv.classList.add('show');
            }
            if (icon) {
                icon.classList.add('show', 'error');
                icon.classList.remove('success');
                icon.textContent = '✕';
            }
            if (formGroup) {
                formGroup.classList.add('has-error');
                formGroup.classList.remove('has-success');
            }
        } else {
            console.warn(`[Phone] Unexpected response:`, data);
        }
    } catch (error) {
        console.error('[Phone] Error:', error);
        if (errorDiv) {
            errorDiv.textContent = 'Error checking phone availability.';
            errorDiv.classList.add('show');
        }
        if (successDiv) successDiv.classList.remove('show');
        if (icon) {
            icon.classList.remove('show');
        }
    }
}

// Debounced phone check
const debouncedPhoneCheck = debounce(checkPhoneAvailability, 500);

// Validate other fields (non-phone)
function validateField(fieldId) {
    const field = document.getElementById(fieldId);
    if (!field) return null;

    const errorDiv = document.getElementById(fieldId + 'Error');
    const successDiv = document.getElementById(fieldId + 'Success');
    const icon = document.getElementById(fieldId + 'Icon');
    const rule = validationRules[fieldId];
    const formGroup = field.parentElement;

    // Empty field
    if (!field.value || !field.value.trim()) {
        if (errorDiv) errorDiv.classList.remove('show');
        if (successDiv) successDiv.classList.remove('show');
        if (icon) icon.classList.remove('show');
        if (formGroup) {
            formGroup.classList.remove('has-error', 'has-success');
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
        const formatValid = rule.pattern.test(field.value);
        if (!formatValid) {
            isValid = false;
            errorMsg = rule.errorMessage;
        } else {
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

    // Update UI
    if (!isValid) {
        if (errorDiv) {
            errorDiv.textContent = errorMsg || 'Invalid input';
            errorDiv.classList.add('show');
        }
        if (successDiv) successDiv.classList.remove('show');
        if (icon) {
            icon.classList.add('show', 'error');
            icon.classList.remove('success');
            icon.textContent = '✕';
        }
        if (formGroup) {
            formGroup.classList.add('has-error');
            formGroup.classList.remove('has-success');
        }
        return false;
    } else {
        if (errorDiv) errorDiv.classList.remove('show');
        if (successDiv) {
            successDiv.textContent = '[OK] Valid ' + fieldId.replace(/([A-Z])/g, ' $1').toLowerCase();
            successDiv.classList.add('show');
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

// Initialize on page load
document.addEventListener('DOMContentLoaded', function () {
    // Clear all validation on load
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

    // ===== PHONE NUMBER INPUT =====
    const phoneNumberInput = document.getElementById('phoneNumber');
    if (phoneNumberInput) {
        phoneNumberInput.addEventListener('input', function (e) {
            // Auto-format as user types
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
            // Call debounced availability check
            debouncedPhoneCheck();
        });
    }

    // ===== IC NUMBER INPUT =====
    const icInput = document.getElementById('ic');
    if (icInput) {
        icInput.addEventListener('input', function (e) {
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
            validateField('ic');
        });

        icInput.addEventListener('blur', function () {
            validateField('ic');
        });
    }

    // ===== NAME, EMAIL, PASSWORD INPUTS =====
    ['name', 'email', 'password'].forEach(fieldId => {
        const field = document.getElementById(fieldId);
        if (field) {
            field.addEventListener('input', function () {
                if (fieldId === 'password') {
                    updatePasswordRequirements(this.value);
                }
                validateField(fieldId);
            });
        }
    });

    // ===== CONFIRM PASSWORD INPUT =====
    const confirmPasswordInput = document.getElementById('confirmPassword');
    if (confirmPasswordInput) {
        confirmPasswordInput.addEventListener('input', function () {
            validateField('confirmPassword');
            const passwordField = document.getElementById('password');
            if (passwordField && passwordField.value) {
                validateField('password');
            }
        });
    }

    // ===== PASSWORD VISIBILITY TOGGLES =====
    const passwordToggle = document.getElementById('passwordToggle');
    const passwordInput = document.getElementById('password');

    if (passwordToggle && passwordInput) {
        passwordToggle.addEventListener('click', function (e) {
            e.preventDefault();
            const isPassword = passwordInput.type === 'password';
            passwordInput.type = isPassword ? 'text' : 'password';

            const icon = this.querySelector('i');
            if (icon) {
                icon.classList.toggle('bi-eye-slash', !isPassword);
                icon.classList.toggle('bi-eye', isPassword);
            }
        });
    }

    const confirmPasswordToggle = document.getElementById('confirmPasswordToggle');
    const confirmPasswordInput2 = document.getElementById('confirmPassword');

    if (confirmPasswordToggle && confirmPasswordInput2) {
        confirmPasswordToggle.addEventListener('click', function (e) {
            e.preventDefault();
            const isPassword = confirmPasswordInput2.type === 'password';
            confirmPasswordInput2.type = isPassword ? 'text' : 'password';

            const icon = this.querySelector('i');
            if (icon) {
                icon.classList.toggle('bi-eye-slash', !isPassword);
                icon.classList.toggle('bi-eye', isPassword);
            }
        });
    }

    // ===== FORM SUBMISSION =====
    const registerForm = document.getElementById('registerForm');
    if (registerForm) {
        registerForm.addEventListener('submit', async (e) => {
            e.preventDefault();

            const fields = ['phoneNumber', 'name', 'ic', 'email', 'password', 'confirmPassword'];
            let allValid = true;

            // Validate all fields
            fields.forEach(fieldId => {
                const field = document.getElementById(fieldId);
                if (!field || !field.value.trim()) {
                    allValid = false;
                } else if (fieldId !== 'phoneNumber') { // Skip phone (has separate check)
                    const result = validateField(fieldId);
                    if (!result) {
                        allValid = false;
                    }
                }
            });

            // Check phone separately (has server check)
            const phoneErrorDiv = document.getElementById('phoneNumberError');
            if (phoneErrorDiv && phoneErrorDiv.classList.contains('show')) {
                allValid = false;
            }

            if (!allValid) {
                const alertContainer = document.getElementById('alertContainer');
                if (alertContainer) {
                    alertContainer.innerHTML = '<div class="alert alert-danger">Please fix all errors before submitting</div>';
                }
                return;
            }

            const submitBtn = document.getElementById('submitBtn');
            if (submitBtn) {
                submitBtn.disabled = true;
                submitBtn.innerHTML = '<span class="spinner"></span>Registering...';
            }

            try {
                const formData = new FormData(registerForm);
                const response = await fetch(registerForm.getAttribute('action') || '/Customer/Auth/Register', {
                    method: 'POST',
                    body: formData,
                    headers: { 'X-Requested-With': 'XMLHttpRequest' }
                });

                const data = await response.json();
                const alertContainer = document.getElementById('alertContainer');

                if (data.success) {
                    if (alertContainer) {
                        alertContainer.innerHTML = '<div class="alert alert-success">' + data.message + '</div>';
                    }
                    registerForm.reset();
                    setTimeout(() => {
                        window.location.href = '/Customer/Auth/Login';
                    }, 1000);
                } else {
                    if (alertContainer) {
                        alertContainer.innerHTML = '<div class="alert alert-danger">' + data.message + '</div>';
                    }

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
                                errorDiv.textContent = Array.isArray(value) ? value[0] : value;
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
                const alertContainer = document.getElementById('alertContainer');
                if (alertContainer) {
                    alertContainer.innerHTML = '<div class="alert alert-danger">An error occurred during registration</div>';
                }
            } finally {
                const submitBtn = document.getElementById('submitBtn');
                if (submitBtn) {
                    submitBtn.disabled = false;
                    submitBtn.innerHTML = 'Register';
                }
            }
        });
    }
});