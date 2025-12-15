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

        newConfirmBtn.addEventListener('click', () => {
            confirmCallback();
            closeConfirmModal();
        });
    };

    const closeConfirmModal = () => {
        const modal = document.getElementById('confirmModal');
        if (modal) modal.classList.remove('show');
        currentForm = null;
    };

    const confirmAdd = (event) => {
        event.preventDefault();
        currentForm = event.target;

        const groomerName = document.getElementById('groomerName')?.value;
        const groomerEmail = document.getElementById('groomerEmail')?.value;
        const giftName = document.getElementById('giftName')?.value;
        const quantity = document.getElementById('quantity')?.value;
        const points = document.getElementById('points')?.value;

        if (groomerName && groomerEmail) {
            showConfirmModal(
                'Add New Groomer?',
                `Are you sure you want to add "${groomerName}"? Login credentials will be sent to ${groomerEmail}.`,
                'person_add',
                '#10b981',
                () => currentForm.submit()
            );
        } else if (giftName && quantity && points) {
            showConfirmModal(
                'Add New Gift?',
                `Are you sure you want to add "${giftName}" (Qty: ${quantity}, Points: ${points})?`,
                'add_circle',
                '#10b981',
                () => currentForm.submit()
            );
        } else {
            alert('Please fill in all required fields');
            return false;
        }

        return false;
    };

    const confirmEdit = (event, name) => {
        event.preventDefault();
        currentForm = event.target;

        showConfirmModal(
            'Save Changes?',
            `Are you sure you want to save changes to "${name}"?`,
            'edit',
            '#f59e0b',
            () => currentForm.submit()
        );

        return false;
    };

    const confirmDelete = (event, name) => {
        event.preventDefault();
        currentForm = event.target;

        const isGroomerPage = document.getElementById('createGroomerForm');
        const itemType = isGroomerPage ? 'Groomer' : 'Gift';

        showConfirmModal(
            `Delete ${itemType}?`,
            `Are you sure you want to delete "${name}"? This action cannot be undone.`,
            'warning',
            '#ef4444',
            () => currentForm.submit()
        );

        return false;
    };

    return {
        init,
        showCreateForm,
        hideCreateForm,
        confirmAdd,
        confirmEdit,
        confirmDelete,
        closeConfirmModal,
        showConfirmModal
    };
})();

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

// Expose submitAddForm for Service page (uses confirm modal flow)
window.submitAddForm = function () {
	const form = document.getElementById('addServiceForm');
	if (!form) return;

	if (!form.checkValidity()) {
		form.reportValidity();
		return;
	}

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
/* ensure confirm modal exists in DOM; create minimal modal if missing */
function ensureConfirmModal() {
 if (document.getElementById('confirmModal')) return;

 const modalHtml = `
 <div id="confirmModal" class="modal-backdrop" style="display:none; position:fixed; inset:0; align-items:center; justify-content:center; z-index:10000;">
 <div class="modal-content" style="background:#fff; border-radius:8px; padding:20px; max-width:450px; width:90%; box-shadow:010px30px rgba(0,0,0,0.2);">
 <span class="close-btn" style="position:absolute; right:12px; top:8px; cursor:pointer; font-size:20px;" onclick="GroomerManager.closeConfirmModal()">&times;</span>
 <div style="text-align:center; padding:10px0;">
 <div style="font-size:48px; color:var(--primary-color); margin-bottom:20px;">
 <i class="material-icons" id="confirmIcon" style="font-size:64px;">help_outline</i>
 </div>
 <h3 id="confirmTitle" style="margin-bottom:15px;">Confirm Action</h3>
 <p id="confirmMessage" style="margin-bottom:30px; color:#666;"></p>
 <div style="display:flex; gap:12px; justify-content:center;">
 <button onclick="GroomerManager.closeConfirmModal()" class="btn btn-action" style="min-width:100px;">Cancel</button>
 <button id="confirmBtn" class="btn btn-primary" style="min-width:100px;">Confirm</button>
 </div>
 </div>
 </div>
 </div>`;

 const container = document.createElement('div');
 container.innerHTML = modalHtml;
 document.body.appendChild(container.firstElementChild);
}
