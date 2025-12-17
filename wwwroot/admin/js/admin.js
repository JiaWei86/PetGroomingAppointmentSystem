﻿// Ensure admin.js initializes only once
if (window.__admin_js_loaded) {
    console.warn('admin.js already loaded, skipping duplicate initialization.');
} else {
    // mark as loaded to prevent duplicate initialization
    window.__admin_js_loaded = true;

    /* ========================================
       SCROLL EFFECT FOR TOPBAR & USER PROFILE
       ======================================== */
    (function () {
        let lastScrollTop = 0;
        const topbar = document.getElementById('topbar');
        const userProfile = document.querySelector('.user-profile');

        function handleScroll() {
            const scrollTop = window.pageYOffset || document.documentElement.scrollTop;

            if (scrollTop > lastScrollTop && scrollTop > 100) {
                topbar?.classList.add('scrolled-down');
                topbar?.classList.remove('scrolled-up');
            } else if (scrollTop < lastScrollTop) {
                topbar?.classList.remove('scrolled-down');
                topbar?.classList.add('scrolled-up');
            }

            if (scrollTop > 50) {
                topbar?.classList.add('scrolled');
                userProfile?.classList.add('scrolled');
            } else {
                topbar?.classList.remove('scrolled');
                userProfile?.classList.remove('scrolled');
            }

            lastScrollTop = scrollTop <= 0 ? 0 : scrollTop;
        }

        window.addEventListener('scroll', handleScroll);
        handleScroll();
    })();

    /* ========================================
       SHARED HELPER FUNCTIONS
       ======================================== */
    function showFieldError(input, errorSpan, message) {
        if (input) {
            input.style.borderColor = '#dc3545';
            input.style.backgroundColor = '#fff5f5';
        }
        if (errorSpan) {
            errorSpan.textContent = message;
            errorSpan.style.display = 'block';
            errorSpan.style.setProperty('color', '#dc3545', 'important');
            errorSpan.style.fontSize = '12px';
            errorSpan.style.marginTop = '5px';
        }
    }

    function showFieldSuccess(input, errorSpan) {
        if (input) {
            input.style.borderColor = '#10b981';
            input.style.backgroundColor = '#f0fdf4';
        }
        if (errorSpan) {
            errorSpan.textContent = '✓ Valid';
            errorSpan.style.display = 'block';
            errorSpan.style.setProperty('color', '#10b981', 'important');
            errorSpan.style.fontSize = '12px';
            errorSpan.style.marginTop = '5px';
        }
    }

    function clearFieldError(inputId, errorSpanId) {
        const input = document.getElementById(inputId);
        const errorSpan = document.getElementById(errorSpanId);
        if (input) {
            input.style.borderColor = '';
            input.style.backgroundColor = '';
        }
        if (errorSpan) {
            errorSpan.textContent = '';
            errorSpan.style.display = 'none';
            errorSpan.style.removeProperty('color');
            errorSpan.style.fontSize = '';
            errorSpan.style.marginTop = '';
        }
    }

    /* ========================================
       FORMATTING HELPERS
       ======================================== */
    function autoFormatIC(input) {
        const cursorPos = input.selectionStart;
        const oldValue = input.value;
        let value = input.value.replace(/\D/g, '');

        if (value.length > 12) {
            value = value.substring(0, 12);
        }

        let formattedValue = value;
        if (value.length > 6) {
            if (value.length > 8) {
                formattedValue = value.substring(0, 6) + '-' + value.substring(6, 8) + '-' + value.substring(8, 12);
            } else {
                formattedValue = value.substring(0, 6) + '-' + value.substring(6);
            }
        }

        if (formattedValue !== oldValue) {
            input.value = formattedValue;

            const digitsBeforeCursor = oldValue.substring(0, cursorPos).replace(/\D/g, '').length;
            let dashesBeforeNew = 0;
            if (digitsBeforeCursor > 6) dashesBeforeNew++;
            if (digitsBeforeCursor > 8) dashesBeforeNew++;

            const newCursorPos = digitsBeforeCursor + dashesBeforeNew;
            input.setSelectionRange(newCursorPos, newCursorPos);
        }
    }

    function autoFormatPhone(input) {
        const cursorPos = input.selectionStart;
        const oldValue = input.value;
        let value = input.value.replace(/\D/g, '');

        if (value.length > 11) {
            value = value.substring(0, 11);
        }

        let formattedValue = value;
        if (value.length > 3) {
            formattedValue = value.substring(0, 3) + '-' + value.substring(3, 11);
        }

        if (formattedValue !== oldValue) {
            input.value = formattedValue;

            const digitsBeforeCursor = oldValue.substring(0, cursorPos).replace(/\D/g, '').length;
            let dashesBeforeNew = 0;
            if (digitsBeforeCursor > 3) dashesBeforeNew++;

            const newCursorPos = digitsBeforeCursor + dashesBeforeNew;
            input.setSelectionRange(newCursorPos, newCursorPos);
        }
    }

    /* ========================================
       PHOTO UPLOAD HANDLER (Shared)
       ======================================== */
    function handlePhotoUpload(input, userId) {
        const statusSpan = document.getElementById(`uploadStatus_${userId}`);

        if (!statusSpan) return;

        if (input.files && input.files.length > 0) {
            const file = input.files[0];
            const fileName = file.name;
            const fileSize = (file.size / 1024 / 1024).toFixed(2);

            statusSpan.style.display = 'block';
            statusSpan.style.fontSize = '12px';
            statusSpan.style.fontWeight = '600';
            statusSpan.style.marginTop = '8px';
            statusSpan.style.textAlign = 'left';

            if (file.size > 5 * 1024 * 1024) {
                statusSpan.textContent = `✗ File too large: ${fileSize}MB (Max 5MB)`;
                statusSpan.style.setProperty('color', '#ef4444', 'important');
                input.value = '';
            } else {
                statusSpan.textContent = `✓ Selected: ${fileName} (${fileSize}MB)`;
                statusSpan.style.setProperty('color', '#10b981', 'important');
            }
        } else {
            statusSpan.style.display = 'none';
            statusSpan.textContent = '';
        }
    }

    /* ========================================
       GROOMER MANAGEMENT MODULE (Shared - Used in Groomer and RedeemGift)
       ======================================== */
    const GroomerManager = (() => {
        let currentForm = null;

        const init = () => {
            const alertMessage = document.getElementById('alertMessage');
            if (alertMessage) {
                setTimeout(() => {
                    alertMessage.style.transition = 'opacity 0.5s';
                    alertMessage.style.opacity = '0';
                    setTimeout(() => alertMessage.remove(), 500);
                }, 5000);
            }

            // Remove confirmBtn listener to avoid conflicts with page-specific modals
            // window.addEventListener('click', (event) => {
            //     const modal = document.getElementById('confirmModal');
            //     if (event.target === modal) closeConfirmModal();
            // });
        };

        const showCreateForm = () => {
            const groomerForm = document.getElementById('createGroomerForm');
            const giftForm = document.getElementById('createGiftForm');
            const serviceForm = document.getElementById('createServiceForm');

            if (serviceForm) {
                serviceForm.style.display = 'block';
            } else if (groomerForm) {
                groomerForm.style.display = 'block';
            } else if (giftForm) {
                giftForm.style.display = 'block';
            }
        };

        const hideCreateForm = () => {
            const groomerForm = document.getElementById('createGroomerForm');
            const giftForm = document.getElementById('createGiftForm');
            const serviceForm = document.getElementById('createServiceForm');

            if (serviceForm) {
                serviceForm.style.display = 'none';
            } else if (groomerForm) {
                groomerForm.style.display = 'none';
            } else if (giftForm) {
                giftForm.style.display = 'none';
            }
        };

        const showConfirmModal = (title, message, icon, iconColor, confirmCallback) => {
            // Ensure modal elements exist before querying them
            try { ensureConfirmModal(); } catch (e) { /* ignore */ }
            const modal = document.getElementById('confirmModal');
            const confirmBtn = document.getElementById('confirmBtn');
            const confirmIcon = document.getElementById('confirmIcon');
            const confirmTitle = document.getElementById('confirmTitle');
            const confirmMessage = document.getElementById('confirmMessage');

            if (!modal || !confirmBtn || !confirmIcon || !confirmTitle || !confirmMessage) {
                console.error('Modal elements not found');
                return;
            }

            confirmTitle.textContent = title;
            confirmMessage.textContent = message;
            confirmIcon.textContent = icon;
            confirmIcon.style.color = iconColor;
            modal.classList.add('show');

            const newConfirmBtn = confirmBtn.cloneNode(true);
            confirmBtn.parentNode.replaceChild(newConfirmBtn, confirmBtn);

            newConfirmBtn.addEventListener('click', (event) => {
                event.preventDefault();
                if (typeof confirmCallback === 'function') {
                    confirmCallback();
                }
                modal.classList.remove('show');
            });

            const closeModalIcon = document.getElementById('closeModal');
            if (closeModalIcon) {
                closeModalIcon.onclick = () => {
                    modal.classList.remove('show');
                };
            }
        };

        const closeConfirmModal = () => {
            const modal = document.getElementById('confirmModal');
            if (modal) {
                modal.classList.remove('show');
            }
        };

        const confirmEdit = (event, name) => {
            event.preventDefault();
            currentForm = event.target;

            showConfirmModal(
                'Save Changes?',
                `Are you sure you want to save changes to "${name}"?`,
                'edit',
                '#f59e0b',
                () => {
                    // Ensure SelectedCategories are copied into hidden inputs before submit (for inline edit forms)
                    try { appendSelectedCategoriesToForm(currentForm); } catch (e) { /* ignore */ }
                    currentForm.submit();
                }
            );

            return false;
        };

        // Confirm add (used by RedeemGift / Service create forms)
        const confirmAdd = (event) => {
            event.preventDefault();
            currentForm = event.target;
            const name = (currentForm.querySelector('[name="Name"]') && currentForm.querySelector('[name="Name"]').value) || 'item';

            showConfirmModal(
                'Confirm Add',
                `Are you sure you want to add "${name}"?`,
                'add',
                '#10b981',
                () => {
                    try { appendSelectedCategoriesToForm(currentForm); } catch (e) { /* ignore */ }
                    currentForm.submit();
                }
            );

            return false;
        };

        // Confirm delete (used by RedeemGift / Groomer / Service delete forms)
        const confirmDelete = (event, name) => {
            // event may be the event or the form element depending on callers; normalize
            if (event && event.preventDefault) event.preventDefault();
            currentForm = (event && event.target) ? event.target : null;

            // If called with just (name) from some pages, try to find a form element in context
            // showConfirmModal will call the callback to submit
            showConfirmModal(
                'Confirm Delete',
                `Are you sure you want to delete "${name}"?`,
                'delete',
                '#ef4444',
                () => {
                    if (currentForm) {
                        currentForm.submit();
                    }
                }
            );

            return false;
        };

        return {
            init,
            showCreateForm,
            hideCreateForm,
            showConfirmModal,
            closeConfirmModal,
            confirmEdit,
            confirmAdd,
            confirmDelete,
        };
    })();

    // This function was defined in some views but should be globally available from admin.js
    window.showDynamicAlert = function (message, type = 'success') {
        const existing = document.getElementById('alertMessage');
        if (existing) existing.remove();

        const alertDiv = document.createElement('div');
        alertDiv.id = 'alertMessage'; // Use the same ID as server-rendered alerts to share styles
        // Apply the same classes as server-rendered alerts for consistent styling
        alertDiv.className = 'alert ' + (type === 'success' ? 'alert-success' : (type === 'warning' ? 'alert-warning' : 'alert-danger'));

        if (type === 'success') alertDiv.innerHTML = `<strong>✔️ Success:</strong> ${message}`;
        else if (type === 'warning') alertDiv.innerHTML = `<strong>⚠️ Warning:</strong> ${message}`;
        else alertDiv.innerHTML = `<strong>❌ Error:</strong> ${message}`;

        document.body.appendChild(alertDiv);

        // Set a timeout to add the fade-out class, then another to remove the element
        setTimeout(function () {
            alertDiv.classList.add('fade-out');
            setTimeout(() => { if (alertDiv && alertDiv.parentNode) { alertDiv.remove(); } }, 500); // Remove after fade-out animation (500ms)
        }, 5000); // Start fading out after 5 seconds
    };

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', GroomerManager.init);
    } else {
        GroomerManager.init();
    }

    window.showCreateForm = GroomerManager.showCreateForm;
    window.hideCreateForm = GroomerManager.hideCreateForm;
    window.confirmAdd = GroomerManager.confirmAdd;
    window.confirmEdit = GroomerManager.confirmEdit;
    window.confirmDelete = GroomerManager.confirmDelete;
    window.closeConfirmModal = GroomerManager.closeConfirmModal;
    window.showConfirmModal = GroomerManager.showConfirmModal;

    // helper: for a given form element, collect checked category checkboxes and append hidden inputs named SelectedCategories
    function appendSelectedCategoriesToForm(form) {
        if (!form) return;

        // remove any previously added hidden inputs (to avoid duplicates)
        Array.from(form.querySelectorAll('input[type="hidden"][data-generated="SelectedCategories"]')).forEach(i => i.remove());

        const checked = form.querySelectorAll('input[type="checkbox"][name="SelectedCategories"]:checked');
        checked.forEach(cb => {
            const hidden = document.createElement('input');
            hidden.type = 'hidden';
            hidden.name = 'SelectedCategories';
            hidden.value = cb.value;
            hidden.setAttribute('data-generated', 'SelectedCategories');
            form.appendChild(hidden);
        });
    }

    // Expose submitAddForm for Service page (uses confirm modal flow)
    window.submitAddForm = function () {
        const form = document.getElementById('addServiceForm');
        if (!form) return;

        if (!form.checkValidity()) {
            form.reportValidity();
            return;
        }

        // Collect checked categories and append hidden inputs so model binding receives them
        appendSelectedCategoriesToForm(form);

        const serviceName = document.getElementById('serviceName')?.value || 'Service';
        // reuse confirmEdit flow: it expects (event, name)
        try {
            window.confirmEdit({ target: form, preventDefault: () => { } }, serviceName);
        } catch (err) {
            // fallback: directly submit
            form.submit();
        }
    };

    /* ========================================
       GLOBAL INITIALIZATION
       ======================================== */
    document.addEventListener('DOMContentLoaded', () => {
        // Auto-hide alerts globally
        const alerts = document.querySelectorAll('.alert');
        alerts.forEach(alert => {
            setTimeout(() => {
                alert.style.opacity = '0';
                setTimeout(() => alert.remove(), 300);
            }, 5000);
        });

        // Global modal close on click outside
        document.addEventListener('click', (e) => {
            const modal = document.getElementById('confirmModal');
            if (modal && e.target === modal) {
                GroomerManager.closeConfirmModal();
            }
        });

        document.addEventListener('keydown', (e) => {
            if (e.key === 'Escape') {
                GroomerManager.closeConfirmModal();
            }
        });
    });

    window.showCreateForm = GroomerManager.showCreateForm;
    window.hideCreateForm = GroomerManager.hideCreateForm;
    window.confirmAdd = GroomerManager.confirmAdd;
    window.confirmEdit = GroomerManager.confirmEdit;
    window.confirmDelete = GroomerManager.confirmDelete;
    window.closeConfirmModal = GroomerManager.closeConfirmModal;
    window.showConfirmModal = GroomerManager.showConfirmModal;

    // Expose submitAddForm for manual use (in case pages call it directly)
    window.submitAddForm = window.submitAddForm || function () {
        const form = document.getElementById('addServiceForm');
        if (!form) return;
        if (!form.checkValidity()) { form.reportValidity(); return; }
        appendSelectedCategoriesToForm(form);
        form.submit();
    };
}
