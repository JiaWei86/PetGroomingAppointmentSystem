
// Select2 initialization for customer search
$(document).ready(function() {
    // Initialize Select2 on the customerSearch select element
    $('#customerSearch').select2({
        placeholder: "Search for a customer by name or phone",
        minimumInputLength: 2,
        allowClear: true, // Option to clear the selection
        ajax: {
            url: '@Url.Action("SearchCustomers", "Home", new { area = "Admin" })',
            dataType: 'json',
            delay: 250, // milliseconds
            data: function (params) {
                return {
                    term: params.term // search term
                };
            },
            processResults: function (data) {
                return {
                    results: $.map(data, function (item) {
                        return {
                            id: item.id,   // customerId
                            text: item.text // customer name (phone)
                        };
                    })
                };
            },
            cache: true
        }
    });

    // When a customer is selected from Select2
    $('#customerSearch').on('select2:select', function (e) {
        const customerId = e.params.data.id;
        $('#customerId').val(customerId); // Update the hidden input
        $('#customerId').trigger('change'); // Trigger change for pet filtering
    });

    // When a customer is unselected from Select2
    $('#customerSearch').on('select2:unselect', function (e) {
        $('#customerId').val(''); // Clear the hidden input
        $('#customerId').trigger('change'); // Trigger change for pet filtering
    });

    let bookingDensityData = {}; // Store booking density data for the current view month

    const fetchBookingDensity = async (flatpickrInstance) => {
        const year = flatpickrInstance.currentYear;
        const month = flatpickrInstance.currentMonth + 1; // flatpickr month is 0-indexed

        try {
            const response = await fetch(`@Url.Action("GetBookingDensity", "Home", new { area = "Admin" })?month=${month}&year=${year}`);
            bookingDensityData = await response.json();
            flatpickrInstance.redraw(); // Redraw the calendar to apply new dots
        } catch (error) {
            console.error('Error fetching booking density:', error);
            bookingDensityData = {};
        }
    };

    // Flatpickr initialization for appointment date and time
    flatpickr("#appointmentDateTime", {
        enableTime: true,
        dateFormat: "Y-m-d H:i",
        altInput: true,
        altFormat: "F j, Y h:i K",
        minDate: "today",
        minuteIncrement: 30,
        minTime: "09:00",
        maxTime: "16:30",
        disable: [
            function(date) {
                // Disable Mondays (day 1 is Monday)
                return date.getDay() === 1;
            }
        ],
        onReady: function(selectedDates, dateStr, instance) {
            fetchBookingDensity(instance);
        },
        onChange: function(selectedDates, dateStr, instance) {
            // When the date changes, we might need to re-fetch or clear if changing months
            // For simplicity, re-fetch on every change if the month changes
            const currentMonth = instance.currentMonth + 1;
            const currentYear = instance.currentYear;
            if (bookingDensityData.month !== currentMonth || bookingDensityData.year !== currentYear) {
                fetchBookingDensity(instance);
            }
        },
        onMonthChange: function(selectedDates, dateStr, instance) {
            fetchBookingDensity(instance);
        },
        onYearChange: function(selectedDates, dateStr, instance) {
            fetchBookingDensity(instance);
        },
        onDayCreate: function(dObj, dStr, fp, dayElem) {
            const day = dObj.getDate();
            const density = bookingDensityData[day];
            if (density) {
                const dot = document.createElement('span');
                dot.classList.add('dot', `dot-${density}`);
                dayElem.appendChild(dot);
            }
        }
    });

    // --- Existing scripts from here ---

    async function deleteAppointment(appointmentId, customerName) {
        const onConfirm = async () => {
            const tokenInput = document.querySelector('form[method="post"] input[name="__RequestVerificationToken"]');
            if (!tokenInput) {
                alert('Error: Anti-forgery token not found. Please refresh the page.');
                return;
            }
            const token = tokenInput.value;

            try {
                const resp = await fetch('@Url.Action("DeleteAppointmentAjax", "Home", new { area = "Admin" })', {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/x-www-form-urlencoded'
                    },
                    body: new URLSearchParams({ 
                        'appointmentId': appointmentId, 
                        '__RequestVerificationToken': token 
                    })
                });

                const data = await resp.json();

                if (data && data.success) {
                    const row = document.querySelector(`tr[data-appointment-id="${appointmentId}"]`);
                    if(row) row.remove();
                    alert(data.message || 'Appointment deleted successfully!');
                } else {
                    alert(data?.message || 'Failed to delete appointment.');
                }
            } catch (err) {
                console.error(err);
                alert('An unexpected error occurred. Please try again.');
            }
        };

        if (window.GroomerManager && typeof window.GroomerManager.showConfirmModal === 'function') {
            GroomerManager.showConfirmModal(
                'Delete Appointment?',
                `Are you sure you want to delete the appointment for ${customerName}? This cannot be undone.`,
                'delete_forever',
                '#ef4444',
                onConfirm
            );
        } else {
            if (confirm(`Are you sure you want to delete the appointment for ${customerName}? This cannot be undone.`)) {
                onConfirm();
            }
        }
    }

    function showCreateForm() {
        const formContainer = document.getElementById('createAppointmentForm');
        const form = formContainer.querySelector('form');

        form.reset();
        formContainer.querySelector('.panel-title').textContent = 'Create New Appointment';
        
        let editIdInput = form.querySelector('input[name="editAppointmentId"]');
        if (editIdInput) {
            editIdInput.remove();
        }
        form.querySelector('input[name="actionType"]').value = 'create';

        // Ensure customerSearch is reset
        $('#customerSearch').val(null).trigger('change');
        $('#customerId').val('');
        $('#customerId').trigger('change'); // Trigger change for pet filtering

        // Clear flatpickr input
        document.getElementById('appointmentDateTime').value = '';

        formContainer.style.display = 'block';
    }

    function hideCreateForm() {
        document.getElementById('createAppointmentForm').style.display = 'none';
    }

    function editAppointment(button) {
        const row = button.closest('tr');
        const formContainer = document.getElementById('createAppointmentForm');
        const form = formContainer.querySelector('form');
        if (!row) return;

        const appointmentId = row.dataset.appointmentId;

        formContainer.querySelector('.panel-title').textContent = 'Edit Appointment';
        form.querySelector('input[name="actionType"]').value = 'edit';

        let editIdInput = form.querySelector('input[name="editAppointmentId"]');
        if (!editIdInput) {
            editIdInput = document.createElement('input');
            editIdInput.type = 'hidden';
            editIdInput.name = 'editAppointmentId';
            form.appendChild(editIdInput);
        }
        editIdInput.value = appointmentId;

        const currentCustomerId = row.dataset.customerId;
        const currentCustomerName = row.querySelector('td:nth-child(3)').textContent.trim(); // Get customer name from table cell
        const currentPetId = row.dataset.petId; // Get current pet ID for pre-selection

        // Set value for Select2
        if ($('#customerSearch').find("option[value='" + currentCustomerId + "']").length) {
            $('#customerSearch').val(currentCustomerId).trigger('change');
        } else {
            // If the option does not exist, manually create and select it (for cases where it wasn't preloaded)
            const newOption = new Option(currentCustomerName, currentCustomerId, true, true);
            $('#customerSearch').append(newOption).trigger('change');
        }
        $('#customerId').val(currentCustomerId); // Set hidden input
        
        // Trigger change and wait for pets to load before setting petId
        const petSelect = document.getElementById('petId');
        const customerIdChangeEvent = new Event('change');
        document.getElementById('customerId').dispatchEvent(customerIdChangeEvent);

        // Add an event listener to 'petId' to detect when options are loaded
        // This is a more robust way than setTimeout
        let observer = new MutationObserver((mutations, obs) => {
            if (petSelect.options.length > 1) { // More than just "Select Pet" option
                petSelect.value = currentPetId;
                obs.disconnect(); // Stop observing once loaded
            }
        });

        observer.observe(petSelect, { childList: true });

        document.getElementById('staffId').value = row.dataset.staffId;
        document.getElementById('serviceId').value = row.dataset.serviceId;
        document.getElementById('status').value = row.dataset.status;
        document.getElementById('specialRequest').value = row.dataset.specialRequest;

        // Set flatpickr value
        const appointmentDateTime = row.dataset.datetime || '';
        document.getElementById('appointmentDateTime').value = appointmentDateTime;

        formContainer.style.display = 'block';
    }

    // Existing pet filtering logic, now listens to the hidden customerId input
    // Existing pet filtering logic, now listens to the hidden customerId input
    document.getElementById('customerId').addEventListener('change', async function() {
        const customerId = this.value;
        const petSelect = document.getElementById('petId');
        
        petSelect.innerHTML = '<option value="">Select Pet</option>'; // Clear existing options

        if (customerId) {
            try {
                const response = await fetch(`@Url.Action("GetPetsByCustomerId", "Home", new { area = "Admin" })?customerId=${customerId}`);
                const pets = await response.json();

                pets.forEach(pet => {
                    const option = document.createElement('option');
                    option.value = pet.id;
                    option.textContent = pet.text;
                    petSelect.appendChild(option);
                });
            } catch (error) {
                console.error('Error fetching pets:', error);
            }
        }
    });
});
