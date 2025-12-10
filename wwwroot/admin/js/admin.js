/* ========================================
   SCROLL EFFECT FOR TOPBAR & USER PROFILE
   ======================================== */
(function() {
    let lastScrollTop = 0;
    const topbar = document.getElementById('topbar');
    const userProfile = document.querySelector('.user-profile');
    
    // Function to handle scroll
    function handleScroll() {
        const scrollTop = window.pageYOffset || document.documentElement.scrollTop;
        
        if (scrollTop > lastScrollTop && scrollTop > 100) {
            // Scrolling down
            topbar?.classList.add('scrolled-down');
            topbar?.classList.remove('scrolled-up');
        } else if (scrollTop < lastScrollTop) {
            // Scrolling up
            topbar?.classList.remove('scrolled-down');
            topbar?.classList.add('scrolled-up');
        }
        
        // Keep user profile position fixed
        if (scrollTop > 50) {
            topbar?.classList.add('scrolled');
            userProfile?.classList.add('scrolled');
        } else {
            topbar?.classList.remove('scrolled');
            userProfile?.classList.remove('scrolled');
        }
        
        lastScrollTop = scrollTop <= 0 ? 0 : scrollTop;
    }
    
    // Add scroll event listener
    window.addEventListener('scroll', handleScroll);
    
    // Check on page load
    handleScroll();
})();

/* ========================================
   ADMIN MANAGEMENT - COMBINED MODULE
   Customer, Pet, and Loyalty Points
   ======================================== */

// ========================================
// CUSTOMER MANAGEMENT MODULE
// ========================================
const CustomerManager = (() => {
    const customerData = {
        1: { id: 1, firstName: "John", lastName: "Doe", email: "john@example.com", phone: "555-1234", address: "123 Main St", city: "New York", postalCode: "10001", createdAt: "Jan 15, 2024 10:30 AM", updatedAt: "Jan 20, 2024 02:15 PM", pets: [{ id: 1, name: "Buddy", type: "Dog", breed: "Golden Retriever", age: 3 }, { id: 3, name: "Max", type: "Dog", breed: "German Shepherd", age: 5 }], loyaltyPoints: 250 },
        2: { id: 2, firstName: "Sarah", lastName: "Johnson", email: "sarah@example.com", phone: "555-5678", address: "456 Oak Ave", city: "Los Angeles", postalCode: "90001", createdAt: "Feb 10, 2024 03:45 PM", updatedAt: "Feb 18, 2024 11:20 AM", pets: [{ id: 2, name: "Whiskers", type: "Cat", breed: "Siamese", age: 2 }], loyaltyPoints: 150 },
        3: { id: 3, firstName: "Michael", lastName: "Smith", email: "michael@example.com", phone: "555-9012", address: "789 Pine Rd", city: "Chicago", postalCode: "60601", createdAt: "Mar 05, 2024 09:00 AM", updatedAt: "Mar 22, 2024 04:30 PM", pets: [], loyaltyPoints: 500 }
    };

    const showCustomerDetails = (customerId) => {
        const customer = customerData[customerId];
        if (!customer) return;

        document.getElementById('viewTitle').textContent = customer.firstName + ' ' + customer.lastName;
        document.getElementById('viewName').textContent = customer.firstName + ' ' + customer.lastName;
        document.getElementById('viewEmail').textContent = customer.email;
        document.getElementById('viewPhone').textContent = customer.phone || 'N/A';
        document.getElementById('viewAddress').textContent = customer.address || 'N/A';
        document.getElementById('viewCity').textContent = customer.city || 'N/A';
        document.getElementById('viewPostalCode').textContent = customer.postalCode || 'N/A';
        document.getElementById('viewCreatedAt').textContent = customer.createdAt;
        document.getElementById('viewUpdatedAt').textContent = customer.updatedAt;

        displayPets(customer.pets);

        document.getElementById('loyaltyPointsValue').textContent = customer.loyaltyPoints + ' pts';
        document.getElementById('loyaltyLastUpdated').textContent = customer.updatedAt;

        switchTab('pets-tab');
        document.getElementById('customerViewModal').classList.add('active');
    };

    const displayPets = (pets) => {
        const petsList = document.getElementById('petsList');
        if (!pets || pets.length === 0) {
            petsList.innerHTML = '<p class="empty-message">No pets assigned</p>';
            return;
        }

        petsList.innerHTML = pets.map(pet => `
            <div class="pet-item">
                <div class="pet-info">
                    <div class="pet-name">${pet.name}</div>
                    <div class="pet-details">${pet.type} · ${pet.breed} · ${pet.age} years old</div>
                </div>
            </div>
        `).join('');
    };

    const switchTab = (tabName) => {
        document.querySelectorAll('.tab-content').forEach(tab => tab.classList.remove('active'));
        document.querySelectorAll('.tab-btn').forEach(btn => btn.classList.remove('active'));

        const selectedTab = document.getElementById(tabName);
        if (selectedTab) selectedTab.classList.add('active');

        const tabBtnMap = { 'pets-tab': 0, 'loyalty-tab': 1 };
        const buttons = document.querySelectorAll('.tab-btn');
        if (buttons[tabBtnMap[tabName]]) {
            buttons[tabBtnMap[tabName]].classList.add('active');
        }
    };

    const closeViewModal = () => {
        document.getElementById('customerViewModal').classList.remove('active');
    };

    const init = () => {
        document.addEventListener('click', (e) => {
            const modal = document.getElementById('customerViewModal');
            if (e.target === modal) closeViewModal();
        });

        document.addEventListener('keydown', (e) => {
            if (e.key === 'Escape') closeViewModal();
        });
    };

    return { init, showCustomerDetails, switchTab, closeViewModal };
})();

// ========================================
// PET MANAGEMENT MODULE
// ========================================
const PetManager = (() => {
    const petData = {
        1: { id: 1, name: "Buddy", petType: "Dog", breed: "Golden Retriever", age: 3, color: "Golden", weight: 30, owner: "John Doe", createdAt: "Jan 15, 2024 10:30 AM", updatedAt: "Jan 20, 2024 02:15 PM" },
        2: { id: 2, name: "Whiskers", petType: "Cat", breed: "Siamese", age: 2, color: "Cream", weight: 4, owner: "Sarah Johnson", createdAt: "Feb 10, 2024 03:45 PM", updatedAt: "Feb 18, 2024 11:20 AM" },
        3: { id: 3, name: "Max", petType: "Dog", breed: "German Shepherd", age: 5, color: "Brown", weight: 35, owner: "John Doe", createdAt: "Mar 05, 2024 09:00 AM", updatedAt: "Mar 22, 2024 04:30 PM" }
    };

    const showPetDetails = (petId) => {
        const pet = petData[petId];
        if (!pet) return;

        document.getElementById('petViewTitle').textContent = pet.name;
        document.getElementById('petViewName').textContent = pet.name;
        document.getElementById('petViewType').textContent = pet.petType;
        document.getElementById('petViewBreed').textContent = pet.breed;
        document.getElementById('petViewAge').textContent = pet.age + ' years';
        document.getElementById('petViewColor').textContent = pet.color;
        document.getElementById('petViewWeight').textContent = pet.weight + ' kg';
        document.getElementById('petViewOwner').textContent = pet.owner;
        document.getElementById('petViewCreatedAt').textContent = pet.createdAt;
        document.getElementById('petViewUpdatedAt').textContent = pet.updatedAt;

        document.getElementById('petViewModal').classList.add('active');
    };

    const closePetViewModal = () => {
        document.getElementById('petViewModal').classList.remove('active');
    };

    const init = () => {
        document.addEventListener('click', (e) => {
            const modal = document.getElementById('petViewModal');
            if (e.target === modal) closePetViewModal();
        });

        document.addEventListener('keydown', (e) => {
            if (e.key === 'Escape') closePetViewModal();
        });
    };

    return { init, showPetDetails, closePetViewModal };
})();

// ========================================
// LOYALTY POINTS MODULE
// ========================================
const LoyaltyManager = (() => {
    const loyaltyData = {
        1: { id: 1, customerName: "John Doe", currentPoints: 250, totalEarned: 500, totalRedeemed: 250, lastTransaction: "Jan 20, 2024 02:15 PM", status: "Active" },
        2: { id: 2, customerName: "Sarah Johnson", currentPoints: 150, totalEarned: 200, totalRedeemed: 50, lastTransaction: "Feb 18, 2024 11:20 AM", status: "Active" },
        3: { id: 3, customerName: "Michael Smith", currentPoints: 500, totalEarned: 600, totalRedeemed: 100, lastTransaction: "Mar 22, 2024 04:30 PM", status: "Active" }
    };

    const showLoyaltyDetails = (loyaltyId) => {
        const loyalty = loyaltyData[loyaltyId];
        if (!loyalty) return;

        document.getElementById('loyaltyDetailTitle').textContent = loyalty.customerName;
        document.getElementById('loyaltyDetailCustomer').textContent = loyalty.customerName;
        document.getElementById('loyaltyDetailCurrent').textContent = loyalty.currentPoints + ' pts';
        document.getElementById('loyaltyDetailEarned').textContent = loyalty.totalEarned + ' pts';
        document.getElementById('loyaltyDetailRedeemed').textContent = loyalty.totalRedeemed + ' pts';
        document.getElementById('loyaltyDetailLastTransaction').textContent = loyalty.lastTransaction;
        document.getElementById('loyaltyDetailStatus').textContent = loyalty.status;

        document.getElementById('loyaltyDetailModal').classList.add('active');
    };

    const closeLoyaltyDetailModal = () => {
        document.getElementById('loyaltyDetailModal').classList.remove('active');
    };

    const init = () => {
        document.addEventListener('click', (e) => {
            const modal = document.getElementById('loyaltyDetailModal');
            if (e.target === modal) closeLoyaltyDetailModal();
        });

        document.addEventListener('keydown', (e) => {
            if (e.key === 'Escape') closeLoyaltyDetailModal();
        });
    };

    return { init, showLoyaltyDetails, closeLoyaltyDetailModal };
})();

// ========================================
// DASHBOARD CHART MODULE
// ========================================
const DashboardChart = (() => {
    let chart = null;
    let currentView = 'week';

    const init = (chartData) => {
        const ctx = document.getElementById('appointmentChart');
        if (!ctx) return;

        chart = new Chart(ctx, {
            type: 'line',
            data: {
                labels: chartData[currentView].labels,
                datasets: [{
                    label: 'Appointments',
                    data: chartData[currentView].data,
                    borderColor: '#d97706',
                    backgroundColor: 'rgba(217, 119, 6, 0.1)',
                    borderWidth: 3,
                    tension: 0.4,
                    fill: true,
                    pointBackgroundColor: '#d97706',
                    pointBorderColor: '#fff',
                    pointBorderWidth: 2,
                    pointRadius: 5,
                    pointHoverRadius: 7
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                plugins: {
                    legend: {
                        display: false
                    },
                    tooltip: {
                        backgroundColor: 'rgba(0, 0, 0, 0.8)',
                        padding: 12,
                        titleColor: '#fff',
                        bodyColor: '#fff',
                        borderColor: '#d97706',
                        borderWidth: 1
                    }
                },
                scales: {
                    y: {
                        beginAtZero: true,
                        grid: {
                            color: 'rgba(0, 0, 0, 0.05)'
                        }
                    },
                    x: {
                        grid: {
                            display: false
                        }
                    }
                }
            }
        });
    };

    const updateChart = (view) => {
        if (!chart || !window.dashboardChartData) return;
        
        currentView = view;
        const data = window.dashboardChartData[view];
        
        chart.data.labels = data.labels;
        chart.data.datasets[0].data = data.data;
        chart.update();

        // Update button states
        document.querySelectorAll('.btn-chart-option').forEach(btn => {
            btn.classList.remove('active');
        });
        event.target.classList.add('active');
    };

    return { init, updateChart };
})();

// Make updateChart global for button onclick
window.updateChart = (view) => DashboardChart.updateChart(view);

// ========================================
// DASHBOARD CALENDAR MODULE
// ========================================
const DashboardCalendar = (() => {
    let currentDate = new Date();
    let selectedDate = new Date();
    let appointments = [];

    const init = (appointmentData) => {
        appointments = appointmentData;
        renderCalendar();
        renderAppointments(selectedDate.toISOString().split('T')[0]);
        
        // Event listeners
        document.getElementById('prevMonth')?.addEventListener('click', () => {
            currentDate.setMonth(currentDate.getMonth() - 1);
            renderCalendar();
        });

        document.getElementById('nextMonth')?.addEventListener('click', () => {
            currentDate.setMonth(currentDate.getMonth() + 1);
            renderCalendar();
        });
    };

    const renderCalendar = () => {
        const calendar = document.getElementById('calendar');
        const monthYear = document.getElementById('monthYear');
        if (!calendar || !monthYear) return;

        const year = currentDate.getFullYear();
        const month = currentDate.getMonth();

        monthYear.textContent = new Date(year, month).toLocaleDateString('en-US', {
            month: 'long',
            year: 'numeric'
        });

        const firstDay = new Date(year, month, 1).getDay();
        const daysInMonth = new Date(year, month + 1, 0).getDate();

        let calendarHTML = '';
        

        // Empty cells before first day
        for (let i = 0; i < firstDay; i++) {
            calendarHTML += '<div class="calendar-day empty"></div>';
        }

        // Days of the month
        for (let day = 1; day <= daysInMonth; day++) {
            const date = new Date(year, month, day);
            const dateStr = date.toISOString().split('T')[0];
            const hasAppointments = appointments.some(apt => apt.date === dateStr);
            const isToday = dateStr === new Date().toISOString().split('T')[0];
            const isSelected = dateStr === selectedDate.toISOString().split('T')[0];

            let classes = 'calendar-day';
            if (isToday) classes += ' today';
            if (isSelected) classes += ' selected';
            if (hasAppointments) classes += ' has-appointments';

            calendarHTML += `
                <div class="${classes}" onclick="selectDate('${dateStr}')">
                    <span class="day-number">${day}</span>
                    ${hasAppointments ? '<span class="appointment-dot"></span>' : ''}
                </div>
            `;
        }

        calendar.innerHTML = calendarHTML;
    };

    const renderAppointments = (dateStr) => {
        const appointmentsList = document.getElementById('appointmentsList');
        const selectedDateBadge = document.getElementById('selectedDate');
        if (!appointmentsList) return;

        const dateAppointments = appointments.filter(apt => apt.date === dateStr);
        
        if (selectedDateBadge) {
            const date = new Date(dateStr);
            selectedDateBadge.textContent = date.toLocaleDateString('en-US', {
                month: 'short',
                day: 'numeric'
            });
        }

        if (dateAppointments.length === 0) {
            appointmentsList.innerHTML = '<p class="no-appointments">No appointments scheduled</p>';
            return;
        }

        appointmentsList.innerHTML = dateAppointments.map(apt => `
            <div class="appointment-item ${apt.status}">
                <div class="appointment-time">${apt.time}</div>
                <div class="appointment-details">
                    <div class="appointment-pet">${apt.petName}</div>
                    <div class="appointment-service">${apt.serviceType}</div>
                    <div class="appointment-groomer">with ${apt.groomerName}</div>
                </div>
                <span class="appointment-status ${apt.status}">${apt.status}</span>
            </div>
        `).join('');
    };

    const selectDate = (dateStr) => {
        selectedDate = new Date(dateStr);
        renderCalendar();
        renderAppointments(dateStr);
    };

    return { init, selectDate };
})();

// Make selectDate global for calendar onclick
window.selectDate = (dateStr) => DashboardCalendar.selectDate(dateStr);

// ========================================
// REPORTS MODULE
// ========================================
function initReportFilters() {
    const reportTypeSelect = document.getElementById('reportType');
    if (reportTypeSelect) {
        reportTypeSelect.addEventListener('change', function() {
            generateReport();
        });
    }

    const dateFromInput = document.getElementById('dateFrom');
    const dateToInput = document.getElementById('dateTo');
    if (dateFromInput && dateToInput) {
        dateFromInput.addEventListener('change', generateReport);
        dateToInput.addEventListener('change', generateReport);
    }
}

function generateReport() {
    const reportType = document.getElementById('reportType')?.value;
    const dateFrom = document.getElementById('dateFrom')?.value;
    const dateTo = document.getElementById('dateTo')?.value;

    console.log(`Generating ${reportType} report from ${dateFrom} to ${dateTo}`);
}

function exportReport(format) {
    const reportType = document.getElementById('reportType')?.value;
    console.log(`Exporting ${reportType} report as ${format}`);
    alert(`Exporting report as ${format.toUpperCase()}...`);
}

// ========================================
// ALERT AUTO-HIDE FUNCTION
// ========================================
function initAlertAutoHide() {
    const alerts = document.querySelectorAll('.alert');
    alerts.forEach(alert => {
        setTimeout(() => {
            alert.style.opacity = '0';
            setTimeout(() => alert.remove(), 300);
        }, 5000);
    });
}

// ========================================
// GROOMER CRUD CONFIRMATION MODULE
// ======================================== 

let currentForm = null;

// Function to show the create form
function showCreateForm() {
    const form = document.getElementById('createGroomerForm');
    if (form) {
        form.style.display = 'block';
    }
}

// Function to hide the create form
function hideCreateForm() {
    const form = document.getElementById('createGroomerForm');
    if (form) {
        form.style.display = 'none';
    }
}

// Show confirmation modal
function showConfirmModal(title, message, icon, iconColor, confirmCallback) {
    const modal = document.getElementById('confirmModal');
    if (!modal) return;

    const confirmBtn = document.getElementById('confirmBtn');
    const confirmIcon = document.getElementById('confirmIcon');
    
    document.getElementById('confirmTitle').textContent = title;
    document.getElementById('confirmMessage').textContent = message;
    confirmIcon.textContent = icon;
    confirmIcon.style.color = iconColor;
    
    modal.classList.add('show');
    
    // Remove previous event listeners
    const newConfirmBtn = confirmBtn.cloneNode(true);
    confirmBtn.parentNode.replaceChild(newConfirmBtn, confirmBtn);
    
    // Add new event listener
    newConfirmBtn.addEventListener('click', () => {
        confirmCallback();
        closeConfirmModal();
    });
}

// Close confirmation modal
function closeConfirmModal() {
    const modal = document.getElementById('confirmModal');
    if (modal) {
        modal.classList.remove('show');
    }
    currentForm = null;
}

// Confirm Add
function confirmAdd(event) {
    event.preventDefault();
    currentForm = event.target;
    
    const groomerName = document.getElementById('groomerName')?.value || 'this groomer';
    const position = document.getElementById('position')?.value || 'Groomer';
    
    showConfirmModal(
        'Add New Groomer?',
        `Are you sure you want to add "${groomerName}" as ${position}?`,
        'add_circle',
        '#10b981',
        () => currentForm.submit()
    );
    
    return false;
}

// Confirm Edit
function confirmEdit(event, groomerName) {
    event.preventDefault();
    currentForm = event.target;
    
    showConfirmModal(
        'Save Changes?',
        `Are you sure you want to save changes to "${groomerName}"?`,
        'edit',
        '#f59e0b',
        () => currentForm.submit()
    );
    
    return false;
}

// Confirm Delete
function confirmDelete(event, groomerName) {
    event.preventDefault();
    currentForm = event.target;
    
    showConfirmModal(
        'Delete Groomer?',
        `Are you sure you want to delete "${groomerName}"? This action cannot be undone.`,
        'warning',
        '#ef4444',
        () => currentForm.submit()
    );
    
    return false;
}

// ========================================
// PHOTO UPLOAD INDICATOR
// ========================================
function handlePhotoUpload(input, userId) {
    const statusSpan = document.getElementById(`uploadStatus_${userId}`);

    if (input.files && input.files.length > 0) {
        const fileName = input.files[0].name;
        const fileSize = (input.files[0].size / 1024 / 1024).toFixed(2);

        statusSpan.style.display = 'block';
        statusSpan.innerHTML = `✓ Uploaded: ${fileName} (${fileSize}MB)`;
        statusSpan.style.color = '#10b981';

        if (input.files[0].size > 5 * 1024 * 1024) {
            statusSpan.innerHTML = `⚠️ File too large: ${fileSize}MB (Max 5MB)`;
            statusSpan.style.color = '#ef4444';
            input.value = '';
        }
    } else {
        statusSpan.style.display = 'none';
    }
}

// ========================================
// INITIALIZATION
// ========================================
document.addEventListener('DOMContentLoaded', () => {
    // Initialize modules
    CustomerManager.init();
    PetManager.init();
    LoyaltyManager.init();

    // Initialize Dashboard Chart with sample data
    if (document.getElementById('appointmentChart')) {
        window.dashboardChartData = {
            week: {
                labels: ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun'],
                data: [12, 15, 8, 14, 18, 10, 6]
            },
            month: {
                labels: ['Week 1', 'Week 2', 'Week 3', 'Week 4'],
                data: [45, 52, 38, 48]
            },
            day: {
                labels: ['9AM', '10AM', '11AM', '12PM', '1PM', '2PM', '3PM', '4PM', '5PM'],
                data: [2, 3, 4, 2, 3, 5, 4, 2, 1]
            }
        };
        DashboardChart.init(window.dashboardChartData);
    }

    // Initialize Dashboard Calendar with sample data
    if (document.getElementById('calendar')) {
        const today = new Date();
        const sampleAppointments = [
            { date: today.toISOString().split('T')[0], time: '9:00 AM', petName: 'Buddy', serviceType: 'Full Grooming', groomerName: 'John Smith', status: 'confirmed' },
            { date: today.toISOString().split('T')[0], time: '11:00 AM', petName: 'Max', serviceType: 'Bath & Brush', groomerName: 'Sarah Lee', status: 'pending' },
            { date: new Date(today.getTime() + 86400000).toISOString().split('T')[0], time: '10:00 AM', petName: 'Whiskers', serviceType: 'Nail Trim', groomerName: 'Mike Johnson', status: 'confirmed' },
            { date: new Date(today.getTime() + 5*86400000).toISOString().split('T')[0], time: '2:00 PM', petName: 'Luna', serviceType: 'Full Grooming', groomerName: 'John Smith', status: 'completed' }
        ];
        DashboardCalendar.init(sampleAppointments);
    }

    // Initialize alert auto-hide
    initAlertAutoHide();

    // Initialize report filters if on reports page
    if (document.getElementById('reportFilters')) {
        initReportFilters();
        generateReport();
    }
});

// Close modal when clicking outside
document.addEventListener('click', (e) => {
    const modal = document.getElementById('confirmModal');
    if (modal && e.target === modal) {
        closeConfirmModal();
    }
});

// Close modal with ESC key
document.addEventListener('keydown', (e) => {
    if (e.key === 'Escape') {
        closeConfirmModal();
    }
});
