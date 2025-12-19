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

// Debounce helper
function debounce(fn, wait) {
    let t;
    return function (...args) {
        const ctx = this;
        clearTimeout(t);
        t = setTimeout(() => fn.apply(ctx, args), wait);
    };
}

// ====== Helper UI functions ======
function setFieldInvalid(errorSpan, input, message) {
    if (errorSpan) {
        errorSpan.textContent = message;
        errorSpan.style.display = 'block';
        errorSpan.style.color = '#dc3545';
    }
    if (input) input.style.borderColor = '#dc3545';
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

function clearFieldError(input, errorSpan) {
    if (input) input.style.borderColor = '';
    if (errorSpan) {
        errorSpan.textContent = '';
        errorSpan.style.display = 'none';
        errorSpan.style.color = '';
    }
}

// ====== Format functions ======
function formatPhoneNumber(input) {
    let value = input.value.replace(/\D/g, '');
    if (value.length > 3) value = value.slice(0, 3) + '-' + value.slice(3);
    if (value.length > 12) value = value.slice(0, 12);
    input.value = value;
}

function formatICNumber(input) {
    const originalValue = input.value;
    const originalCursorPos = input.selectionStart;

    const dashesBeforeCursorOld = (originalValue.substring(0, originalCursorPos).match(/-/g) || []).length;

    let digits = originalValue.replace(/\D/g, '');

    if (digits.length > 12) {
        digits = digits.substring(0, 12);
    }

    let formattedValue = digits;
    if (digits.length > 6) {
        formattedValue = `${digits.substring(0, 6)}-${digits.substring(6, 8)}`;
        if (digits.length > 8) {
            formattedValue += `-${digits.substring(8)}`;
        }
    }

    const dashesBeforeCursorNew = (formattedValue.substring(0, originalCursorPos).match(/-/g) || []).length;
    const newCursorPos = originalCursorPos + (dashesBeforeCursorNew - dashesBeforeCursorOld);

    input.value = formattedValue;
    input.setSelectionRange(newCursorPos, newCursorPos);
}

// ====== Validation functions ======
async function validatePhoneNumber(value) {
    const errorSpan = document.getElementById('phoneNumberError');
    const input = document.getElementById('phoneNumber');
    if (!input) return true;

    clearFieldError(input, errorSpan);

    const phoneRegex = /^01[0-9]-[0-9]{7,8}$/;
    if (!value || !phoneRegex.test(value)) {
        setFieldInvalid(errorSpan, input, 'Phone must be in format 01X-XXXXXXX or 01X-XXXXXXXX.');
        return false;
    }

    try {
        const resp = await fetch('/Customer/Auth/ValidateRegisterField', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                fieldName: 'phoneNumber',
                fieldValue: value
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
        console.error('Phone validation error', e);
        setFieldInvalid(errorSpan, input, 'Validation check failed.');
        return false;
    }
}

async function validateName(value) {
    const errorSpan = document.getElementById('nameError');
    const input = document.getElementById('name');
    if (!input) return true;

    clearFieldError(input, errorSpan);

    if (!value || value.trim().length < 3) {
        setFieldInvalid(errorSpan, input, 'Name must be at least 3 characters long.');
        return false;
    }

    try {
        const resp = await fetch('/Customer/Auth/ValidateRegisterField', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                fieldName: 'name',
                fieldValue: value
            })
        });
        const data = await resp.json();
        if (data.isValid) {
            setFieldValid(errorSpan, input, 'Valid');
            return true;
        } else {
            setFieldInvalid(errorSpan, input, data.errorMessage || 'Invalid name.');
            return false;
        }
    } catch (e) {
        console.error('Name validation error', e);
        setFieldInvalid(errorSpan, input, 'Validation check failed.');
        return false;
    }
}

async function validateIC(value) {
    const errorSpan = document.getElementById('icError');
    const input = document.getElementById('ic');
    if (!input) return true;

    clearFieldError(input, errorSpan);

    const icRegex = /^\d{6}-\d{2}-\d{4}$/;
    if (!value || !icRegex.test(value)) {
        setFieldInvalid(errorSpan, input, 'IC must be in format xxxxxx-xx-xxxx.');
        return false;
    }

    try {
        const resp = await fetch('/Customer/Auth/ValidateRegisterField', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                fieldName: 'ic',
                fieldValue: value
            })
        });
        const data = await resp.json();
        if (data.isValid) {
            setFieldValid(errorSpan, input, 'Valid');
            return true;
        } else {
            setFieldInvalid(errorSpan, input, data.errorMessage || 'IC validation failed.');
            return false;
        }
    } catch (e) {
        console.error('IC validation error', e);
        setFieldInvalid(errorSpan, input, 'Validation check failed.');
        return false;
    }
}

async function validateEmail(value) {
    const errorSpan = document.getElementById('emailError');
    const input = document.getElementById('email');
    if (!input) return true;

    clearFieldError(input, errorSpan);

    const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
    if (!value || !emailRegex.test(value)) {
        setFieldInvalid(errorSpan, input, 'Please enter a valid email address.');
        return false;
    }

    try {
        const resp = await fetch('/Customer/Auth/ValidateRegisterField', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                fieldName: 'email',
                fieldValue: value
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
        console.error('Email validation error', e);
        setFieldInvalid(errorSpan, input, 'Validation check failed.');
        return false;
    }
}

async function validatePassword(value) {
    const errorSpan = document.getElementById('passwordError');
    const input = document.getElementById('password');
    if (!input) return true;

    clearFieldError(input, errorSpan);

    if (!value || value.length < 8) {
        setFieldInvalid(errorSpan, input, 'Password must be at least 8 characters.');
        return false;
    }

    try {
        const resp = await fetch('/Customer/Auth/ValidateRegisterField', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                fieldName: 'password',
                fieldValue: value
            })
        });
        const data = await resp.json();
        if (data.isValid) {
            setFieldValid(errorSpan, input, 'Valid');
            return true;
        } else {
            setFieldInvalid(errorSpan, input, data.errorMessage || 'Password validation failed.');
            return false;
        }
    } catch (e) {
        console.error('Password validation error', e);
        setFieldInvalid(errorSpan, input, 'Validation check failed.');
        return false;
    }
}

function validateConfirmPassword(value) {
    const errorSpan = document.getElementById('confirmPasswordError');
    const input = document.getElementById('confirmPassword');
    const passwordInput = document.getElementById('password');
    if (!input) return true;

    clearFieldError(input, errorSpan);

    if (!value) {
        // Don't show error if empty, just clear validation
        return false;
    }

    if (value !== passwordInput.value) {
        setFieldInvalid(errorSpan, input, 'Passwords do not match.');
        return false;
    }

    setFieldValid(errorSpan, input, 'Passwords match');
    return true;
}

function updatePasswordRequirements(password) {
    const strength = {
        length: password.length >= 8,
        uppercase: /[A-Z]/.test(password),
        lowercase: /[a-z]/.test(password),
        symbol: /[!@#$%^&*()_+\-=\[\]{};':"\\|,.<>\/?]/.test(password)
    };

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

// ====== Debounced validations ======
const validatePhoneDebounced = debounce(() => {
    const phoneInput = document.getElementById('phoneNumber');
    if (phoneInput && phoneInput.value.trim()) {
        validatePhoneNumber(phoneInput.value);
    }
}, 30);

const validateNameDebounced = debounce(() => {
    const nameInput = document.getElementById('name');
    if (nameInput && nameInput.value.trim()) {
        validateName(nameInput.value);
    }
}, 30);

const validateICDebounced = debounce(() => {
    const icInput = document.getElementById('ic');
    if (icInput && icInput.value.trim()) {
        validateIC(icInput.value);
    }
}, 30);

const validateEmailDebounced = debounce(() => {
    const emailInput = document.getElementById('email');
    if (emailInput && emailInput.value.trim()) {
        validateEmail(emailInput.value);
    }
}, 30);

const validatePasswordDebounced = debounce(() => {
    const passwordInput = document.getElementById('password');
    if (passwordInput && passwordInput.value) {
        validatePassword(passwordInput.value);
    }
}, 30);

// ====== Initialize on page load ======
document.addEventListener('DOMContentLoaded', function () {
    // ===== PHONE NUMBER INPUT =====
    const phoneNumberInput = document.getElementById('phoneNumber');
    if (phoneNumberInput) {
        phoneNumberInput.addEventListener('input', function () {
            formatPhoneNumber(this);
            validatePhoneDebounced();
        });
        phoneNumberInput.addEventListener('blur', function () {
            if (this.value.trim()) validatePhoneNumber(this.value);
        });
    }

    // ===== NAME INPUT =====
    const nameInput = document.getElementById('name');
    if (nameInput) {
        nameInput.addEventListener('input', function () {
            validateNameDebounced();
        });
        nameInput.addEventListener('blur', function () {
            if (this.value.trim()) validateName(this.value);
        });
    }

    // ===== IC INPUT =====
    const icInput = document.getElementById('ic');
    if (icInput) {
        icInput.addEventListener('input', function () {
            formatICNumber(this);
            validateICDebounced();
        });
        icInput.addEventListener('blur', function () {
            if (this.value.trim()) validateIC(this.value);
        });
    }

    // ===== EMAIL INPUT =====
    const emailInput = document.getElementById('email');
    if (emailInput) {
        emailInput.addEventListener('input', function () {
            validateEmailDebounced();
        });
        emailInput.addEventListener('blur', function () {
            if (this.value.trim()) validateEmail(this.value);
        });
    }

    // ===== PASSWORD INPUT =====
    const passwordInput = document.getElementById('password');
    if (passwordInput) {
        passwordInput.addEventListener('input', function () {
            updatePasswordRequirements(this.value);
            validatePasswordDebounced();
        });
        passwordInput.addEventListener('blur', function () {
            if (this.value) validatePassword(this.value);
        });
    }

    // ===== CONFIRM PASSWORD INPUT =====
    const confirmPasswordInput = document.getElementById('confirmPassword');
    if (confirmPasswordInput) {
        confirmPasswordInput.addEventListener('input', function () {
            validateConfirmPassword(this.value);
        });
    }

    // ===== PASSWORD VISIBILITY TOGGLE =====
    const passwordToggle = document.getElementById('passwordToggle');
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

            const submitBtn = document.getElementById('submitBtn');
            if (submitBtn) {
                submitBtn.disabled = true;
                submitBtn.innerHTML = 'Registering...';
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
                        // ✅ 使用服务器返回的重定向 URL（包含 registered 参数）
                        window.location.href = data.redirectUrl;
                    }, 1500);
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
                            const errorSpan = document.getElementById(fieldId + 'Error');
                            const field = document.getElementById(fieldId);
                            setFieldInvalid(errorSpan, field, Array.isArray(value) ? value[0] : value);
                        }
                    }
                }
            } catch (error) {
                const alertContainer = document.getElementById('alertContainer');
                if (alertContainer) {
                    alertContainer.innerHTML = '<div class="alert alert-danger">An error occurred during registration</div>';
                }
                
                const submitBtn = document.getElementById('submitBtn');
                if (submitBtn) {
                    submitBtn.disabled = false;
                    submitBtn.innerHTML = 'Register';
                }
            }
        });
    }
});