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
        document.getElementById('viewPetName').textContent = pet.name;
        document.getElementById('viewPetOwner').textContent = pet.owner;
        document.getElementById('viewPetType').textContent = pet.petType;
        document.getElementById('viewPetBreed').textContent = pet.breed;
        document.getElementById('viewPetAge').textContent = pet.age + ' years';
        document.getElementById('viewPetColor').textContent = pet.color;
        document.getElementById('viewPetWeight').textContent = pet.weight + ' kg';
        document.getElementById('viewPetCreatedAt').textContent = pet.createdAt;
        document.getElementById('viewPetUpdatedAt').textContent = pet.updatedAt;

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
// LOYALTY POINTS MANAGEMENT MODULE
// ========================================
const LoyaltyPointManager = (() => {
    const loyaltyData = {
        1: { id: 1, customer: "John Doe", email: "john@example.com", balance: 250, createdAt: "Jan 15, 2024 10:30 AM", updatedAt: "Jan 20, 2024 02:15 PM" },
        2: { id: 2, customer: "Sarah Johnson", email: "sarah@example.com", balance: 150, createdAt: "Feb 10, 2024 03:45 PM", updatedAt: "Feb 18, 2024 11:20 AM" },
        3: { id: 3, customer: "Michael Smith", email: "michael@example.com", balance: 500, createdAt: "Mar 05, 2024 09:00 AM", updatedAt: "Mar 22, 2024 04:30 PM" }
    };

    const viewPoints = (loyaltyId) => {
        const loyalty = loyaltyData[loyaltyId];
        if (!loyalty) return;

        document.getElementById('pointsViewTitle').textContent = `${loyalty.customer} - Loyalty Points`;
        document.getElementById('viewPointsCustomer').textContent = loyalty.customer;
        document.getElementById('viewPointsEmail').textContent = loyalty.email;
        document.getElementById('viewPointsBalance').textContent = `${loyalty.balance} points`;
        document.getElementById('viewPointsCreatedAt').textContent = loyalty.createdAt;
        document.getElementById('viewPointsUpdatedAt').textContent = loyalty.updatedAt;

        document.getElementById('pointsViewModal').classList.add('active');
    };

    const closePointsViewModal = () => {
        document.getElementById('pointsViewModal').classList.remove('active');
    };

    const init = () => {
        document.addEventListener('click', (e) => {
            const modal = document.getElementById('pointsViewModal');
            if (e.target === modal) closePointsViewModal();
        });

        document.addEventListener('keydown', (e) => {
            if (e.key === 'Escape') closePointsViewModal();
        });
    };

    return { init, viewPoints, closePointsViewModal };
})();

// ========================================
// INITIALIZE ALL MANAGEMENT MODULES
// ========================================
document.addEventListener('DOMContentLoaded', () => {
    CustomerManager.init();
    PetManager.init();
    LoyaltyPointManager.init();
});

// expose to global scope
window.showCustomerDetails = CustomerManager.showCustomerDetails;
window.switchTab = CustomerManager.switchTab;
window.closeViewModal = CustomerManager.closeViewModal;

window.showPetDetails = PetManager.showPetDetails;
window.closePetViewModal = PetManager.closePetViewModal;

window.viewPoints = LoyaltyPointManager.viewPoints;
window.closePointsViewModal = LoyaltyPointManager.closePointsViewModal;

// ========================================
// DASHBOARD CALENDAR MODULE
// ========================================
const DashboardCalendar = (() => {
    let currentDate = new Date();
    let appointmentData = [];

    const setAppointmentData = (data) => {
        appointmentData = data;
    };

    const formatDate = (dateStr) => {
        const date = new Date(dateStr);
        const today = new Date();
        const tomorrow = new Date(today);
        tomorrow.setDate(tomorrow.getDate() + 1);

        if (date.toDateString() === today.toDateString()) return 'Today';
        if (date.toDateString() === tomorrow.toDateString()) return 'Tomorrow';
        const options = { month: 'short', day: 'numeric', year: 'numeric' };
        return date.toLocaleDateString('en-US', options);
    };

    const renderCalendar = () => {
        const year = currentDate.getFullYear();
        const month = currentDate.getMonth();

        const monthNames = ["January", "February", "March", "April", "May", "June",
            "July", "August", "September", "October", "November", "December"];
        document.getElementById('monthYear').textContent = monthNames[month] + ' ' + year;

        const firstDay = new Date(year, month, 1).getDay();
        const daysInMonth = new Date(year, month + 1, 0).getDate();

        let calendarHTML = '<div class="day-names">';
        const dayNames = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'];
        dayNames.forEach(day => {
            calendarHTML += `<div class="day-name">${day}</div>`;
        });
        calendarHTML += '</div>';

        calendarHTML += '<div class="calendar-dates">';

        for (let i = 0; i < firstDay; i++) calendarHTML += '<div class="empty-day"></div>';

        for (let day = 1; day <= daysInMonth; day++) {
            const dateStr = `${year}-${String(month + 1).padStart(2, '0')}-${String(day).padStart(2, '0')}`;
            const dayAppointments = appointmentData.filter(a => a.date === dateStr);
            const hasAppointments = dayAppointments.length > 0;

            calendarHTML += `
                <div class="calendar-day ${hasAppointments ? 'has-appointments' : ''}" data-date="${dateStr}">
                    <div class="day-number">${day}</div>
                    ${hasAppointments ? `<div class="appointments-count">${dayAppointments.length}</div>` : ''}
                </div>
            `;
        }

        calendarHTML += '</div>';
        document.getElementById('calendar').innerHTML = calendarHTML;

        document.querySelectorAll('.calendar-day').forEach(day => {
            day.addEventListener('click', function () {
                const date = this.getAttribute('data-date');
                showAppointmentsForDate(date);
                document.querySelectorAll('.calendar-day').forEach(d => d.classList.remove('selected'));
                this.classList.add('selected');
            });
        });

        const today = new Date();
        const todayStr = `${today.getFullYear()}-${String(today.getMonth() + 1).padStart(2, '0')}-${String(today.getDate()).padStart(2, '0')}`;
        showAppointmentsForDate(todayStr);
    };

    const showAppointmentsForDate = (dateStr) => {
        const dayAppointments = appointmentData.filter(a => a.date === dateStr);
        const appointmentsList = document.getElementById('appointmentsList');
        const selectedDateBadge = document.getElementById('selectedDate');

        if (selectedDateBadge) selectedDateBadge.textContent = formatDate(dateStr);

        if (dayAppointments.length === 0) {
            appointmentsList.innerHTML = `
                <div class="no-appointments">
                    <i class="material-icons" style="font-size: 48px; color: #d1d5db; margin-bottom: 10px;">event_busy</i>
                    <p>No appointments scheduled</p>
                    <small style="color: var(--text-light);">${formatDate(dateStr)}</small>
                </div>
            `;
            return;
        }

        let html = '<div class="appointments-group">';
        dayAppointments.forEach(appointment => {
            const statusIcon = appointment.status === 'confirmed' ? 'check_circle' :
                appointment.status === 'pending' ? 'schedule' : 'cancel';

            html += `
                <div class="appointment-item ${appointment.status}">
                    <div class="appointment-time">
                        <i class="material-icons" style="font-size: 16px; vertical-align: middle;">access_time</i>
                        ${appointment.time}
                    </div>
                    <div class="appointment-details">
                        <div class="appointment-pet">
                            <i class="material-icons" style="font-size: 14px; vertical-align: middle;">pets</i>
                            ${appointment.petName}
                        </div>
                        <div class="appointment-groomer">
                            <i class="material-icons" style="font-size: 14px; vertical-align: middle;">person</i>
                            ${appointment.groomerName}
                        </div>
                        <div class="appointment-service">
                            <i class="material-icons" style="font-size: 14px; vertical-align: middle;">content_cut</i>
                            ${appointment.serviceType}
                        </div>
                    </div>
                    <div class="appointment-status">
                        <i class="material-icons" style="font-size: 14px; vertical-align: middle;">${statusIcon}</i>
                        ${appointment.status.charAt(0).toUpperCase() + appointment.status.slice(1)}
                    </div>
                </div>
            `;
        });
        html += '</div>';
        appointmentsList.innerHTML = html;
    };

    const prevMonth = () => {
        currentDate.setMonth(currentDate.getMonth() - 1);
        renderCalendar();
    };

    const nextMonth = () => {
        currentDate.setMonth(currentDate.getMonth() + 1);
        renderCalendar();
    };

    const init = (appointments = []) => {
        appointmentData = appointments;
        const prevBtn = document.getElementById('prevMonth');
        const nextBtn = document.getElementById('nextMonth');
        if (prevBtn) prevBtn.addEventListener('click', prevMonth);
        if (nextBtn) nextBtn.addEventListener('click', nextMonth);
        if (document.getElementById('calendar')) renderCalendar();
    };

    return { init, setAppointmentData, renderCalendar };
})();

window.DashboardCalendar = DashboardCalendar;

// ========================================
// DASHBOARD CHART MODULE
// ========================================
const DashboardChart = (() => {
    let chart = null;

    const init = (chartData) => {
        const ctx = document.getElementById('appointmentChart');
        if (!ctx) return;

        chart = new Chart(ctx.getContext('2d'), {
            type: 'line',
            data: {
                labels: chartData.week.labels,
                datasets: [{
                    label: 'Appointments',
                    data: chartData.week.data,
                    backgroundColor: 'rgba(217, 119, 6, 0.1)',
                    borderColor: 'rgba(217, 119, 6, 1)',
                    borderWidth: 3,
                    fill: true,
                    tension: 0.4,
                    pointBackgroundColor: 'rgba(217, 119, 6, 1)',
                    pointBorderColor: '#fff',
                    pointBorderWidth: 2,
                    pointRadius: 5,
                    pointHoverRadius: 7
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: true,
                plugins: {
                    legend: { display: false },
                    tooltip: {
                        backgroundColor: 'rgba(31, 41, 55, 0.9)',
                        padding: 12,
                        titleColor: '#fff',
                        bodyColor: '#fff',
                        borderColor: 'rgba(217, 119, 6, 0.5)',
                        borderWidth: 1,
                        displayColors: false,
                        callbacks: {
                            label: function (context) {
                                return 'Appointments: ' + context.parsed.y;
                            }
                        }
                    }
                },
                scales: {
                    y: {
                        beginAtZero: true,
                        ticks: { stepSize: 5, color: '#6b7280' },
                        grid: { color: 'rgba(229, 231, 235, 0.5)', drawBorder: false }
                    },
                    x: {
                        ticks: { color: '#6b7280' },
                        grid: { display: false, drawBorder: false }
                    }
                }
            }
        });
    };

    const update = (period, chartData) => {
        if (!chart) return;
        const data = chartData[period];
        chart.data.labels = data.labels;
        chart.data.datasets[0].data = data.data;
        chart.update();
    };

    return { init, update };
})();

window.DashboardChart = DashboardChart;
window.updateChart = (period, chartData) => {
    document.querySelectorAll('.btn-chart-option').forEach(btn => btn.classList.remove('active'));
    event.target.classList.add('active');
    DashboardChart.update(period, window.dashboardChartData);
};

/* ========================================
   REPORTS & ANALYTICS MODULE
   ======================================== */

// Revenue chart (if you still use it in reports page)
let revenueChart = null;
const revenueData = {
    labels: ['Full Groom', 'Bath & Trim', 'Nail Clipping', 'Teeth Cleaning', 'Ear Cleaning'],
    datasets: [{
        label: 'Revenue (RM)',
        data: [13500, 7600, 4160, 3200, 2800],
        backgroundColor: [
            'rgba(217, 119, 6, 0.8)',
            'rgba(16, 185, 129, 0.8)',
            'rgba(245, 158, 11, 0.8)',
            'rgba(239, 68, 68, 0.8)',
            'rgba(59, 130, 246, 0.8)'
        ],
        borderColor: [
            'rgba(217, 119, 6, 1)',
            'rgba(16, 185, 129, 1)',
            'rgba(245, 158, 11, 1)',
            'rgba(239, 68, 68, 1)',
            'rgba(59, 130, 246, 1)'
        ],
        borderWidth: 2
    }]
};

function initRevenueChart() {
    const ctx = document.getElementById('revenueChart');
    if (!ctx) return;

    revenueChart = new Chart(ctx.getContext('2d'), {
        type: 'bar',
        data: revenueData,
        options: {
            responsive: true,
            maintainAspectRatio: true,
            plugins: {
                legend: { display: false },
                tooltip: {
                    backgroundColor: 'rgba(31, 41, 55, 0.9)',
                    padding: 12,
                    titleColor: '#fff',
                    bodyColor: '#fff',
                    borderColor: 'rgba(217, 119, 6, 0.5)',
                    borderWidth: 1,
                    callbacks: {
                        label: function (context) {
                            return 'Revenue: RM ' + context.parsed.y.toLocaleString();
                        }
                    }
                }
            },
            scales: {
                y: {
                    beginAtZero: true,
                    ticks: {
                        color: '#6b7280',
                        callback: function (value) {
                            return 'RM ' + value.toLocaleString();
                        }
                    },
                    grid: { color: 'rgba(229, 231, 235, 0.5)', drawBorder: false }
                },
                x: {
                    ticks: { color: '#6b7280' },
                    grid: { display: false, drawBorder: false }
                }
            }
        }
    });
}


// ====== Reports page interactive data ======
const reportData = {
    revenue: {
        title: 'Revenue Report',
        subtitle: 'Revenue by service & date',
        summary: [
            { label: 'Total Revenue', value: 'RM 45,280', note: '+12.5% vs last period' },
            { label: 'Completed Appointments', value: '168', note: '+8.2% vs last period' },
            { label: 'New Customers', value: '24', note: '+15% vs last period' },
            { label: 'Average Rating', value: '4.8 / 5.0', note: '+0.3 improvement' }
        ],
        columns: ['Date', 'Service Type', 'Appointments', 'Revenue (RM)', 'Growth'],
        rows: [
            ['2024-12-01', 'Full Groom', '12', '3,600', '+8%'],
            ['2024-12-01', 'Bath & Trim', '8', '1,600', '+12%'],
            ['2024-12-01', 'Nail Clipping', '15', '450', '-3%'],
            ['2024-11-30', 'Full Groom', '10', '3,000', '+5%'],
            ['2024-11-30', 'Bath & Trim', '6', '1,200', '+10%']
        ]
    },
    appointments: {
        title: 'Appointments Report',
        subtitle: 'Appointment details with status',
        summary: [
            { label: 'Total Appointments', value: '210', note: '+5.4% vs last period' },
            { label: 'Completed', value: '168', note: '80% completion' },
            { label: 'Pending', value: '32', note: 'Need confirmation' },
            { label: 'Cancelled', value: '10', note: 'Down 2% vs last period' }
        ],
        columns: ['Date', 'Customer', 'Pet', 'Service', 'Groomer', 'Amount (RM)', 'Status'],
        rows: [
            ['2024-12-01', 'John Doe', 'Buddy', 'Full Groom', 'Anna Lee', '300', 'Completed'],
            ['2024-12-01', 'Sarah Johnson', 'Whiskers', 'Bath', 'Mark Chen', '150', 'Completed'],
            ['2024-12-01', 'Michael Smith', 'Max', 'Nail Trim', 'Anna Lee', '80', 'Completed']
        ]
    },
    services: {
        title: 'Services Report',
        subtitle: 'Service performance snapshot',
        summary: [
            { label: 'Top Service', value: 'Full Groom', note: '45 bookings' },
            { label: 'Fastest Growing', value: 'Bath & Trim', note: '+12% revenue' },
            { label: 'Highest Margin', value: 'Spa Package', note: 'RM 280 avg' },
            { label: 'Avg. Ticket', value: 'RM 230', note: '+5% vs last' }
        ],
        columns: ['Service Name', 'Total Bookings', 'Revenue (RM)', 'Avg. Price (RM)', 'Popularity'],
        rows: [
            ['Full Groom', '45', '13,500', '300', 'High'],
            ['Bath & Trim', '38', '7,600', '200', 'High'],
            ['Nail Clipping', '52', '4,160', '80', 'Medium']
        ]
    },
    customers: {
        title: 'Customers Report',
        subtitle: 'Customer loyalty and spend analysis',
        summary: [
            { label: 'Active Customers', value: '340', note: '+4.5% vs last period' },
            { label: 'High Value', value: '68', note: 'Spend > RM3k' },
            { label: 'Churn Risk', value: '24', note: 'No visit 90 days' },
            { label: 'Avg. Lifetime Value', value: 'RM 2,150', note: 'Sample data' }
        ],
        columns: ['Customer Name', 'Total Visits', 'Total Spent (RM)', 'Loyalty Points', 'Last Visit'],
        rows: [
            ['John Doe', '12', '3,600', '250', '2024-12-01'],
            ['Sarah Johnson', '8', '1,600', '150', '2024-11-30'],
            ['Michael Smith', '15', '4,500', '500', '2024-11-29']
        ]
    },
    groomers: {
        title: 'Groomer Performance Report',
        subtitle: 'Staff utilization and performance summary',
        summary: [
            { label: 'Top Groomer', value: 'Anna Lee', note: '45 appointments' },
            { label: 'Avg. Completion Time', value: '1h 15m', note: '-5% vs last period' },
            { label: 'Customer Rating', value: '4.85 / 5', note: '+0.2 improvement' },
            { label: 'Total Incentives', value: 'RM 4,200', note: 'Sample data' }
        ],
        columns: ['Groomer', 'Appointments', 'Avg. Rating', 'Revenue (RM)', 'Specialty'],
        rows: [
            ['Anna Lee', '45', '4.9', '13,500', 'Full Groom'],
            ['Mark Chen', '32', '4.7', '8,200', 'Bath & Trim'],
            ['Lisa Wong', '28', '4.6', '6,800', 'Spa & Styling']
        ]
    }
};

function buildTable(columns, rows, type) {
    const tableHead = columns.map((col) => {
        const colLower = col.toLowerCase();
        const alignRight = colLower.includes('amount') || colLower.includes('revenue') || colLower.includes('price') || colLower.includes('visits') || colLower.includes('spent') || colLower.includes('appointments');
        return `<th${alignRight ? ' class="text-right"' : ''}>${col}</th>`;
    }).join('');

    const tableBody = rows.map((row) => {
        return '<tr>' + row.map((cell, idx) => {
            const colName = columns[idx].toLowerCase();
            const alignRight = colName.includes('amount') || colName.includes('revenue') ||
                colName.includes('price') || colName.includes('visits') || colName.includes('spent') || colName.includes('appointments');
            let value = cell;

            if (type === 'appointments' && colName === 'status') {
                value = `<span class="status-tag ${cell.toLowerCase()}">${cell}</span>`;
            }
            if (type === 'revenue' && colName === 'growth') {
                const isUp = cell.startsWith('+');
                value = `<span class="${isUp ? 'text-success' : 'text-danger'}">${cell}</span>`;
            }

            return `<td${alignRight ? ' class="text-right"' : ''}>${value}</td>`;
        }).join('') + '</tr>';
    }).join('');

    return `
        <div class="table-responsive">
            <table class="data-table report-table">
                <thead><tr>${tableHead}</tr></thead>
                <tbody>${tableBody}</tbody>
            </table>
        </div>
    `;
}

function renderSummaryCards(summary) {
    const summaryContainer = document.getElementById('reportSummary');
    if (!summaryContainer) return;
    summaryContainer.innerHTML = summary.map(item => `
        <div class="summary-card">
            <div class="summary-label">${item.label}</div>
            <div class="summary-value">${item.value}</div>
            <div class="summary-note">${item.note}</div>
        </div>
    `).join('');
}

function updateReportHeader(config, range, start, end) {
    const label = document.getElementById('currentDateRangeLabel');
    const title = document.getElementById('reportTitle');
    const subtitle = document.getElementById('currentReportTitle');
    const timestamp = document.getElementById('generatedTimestamp');

    if (title) title.textContent = config.title;
    if (subtitle) subtitle.textContent = `${config.subtitle} (${range})`;
    if (timestamp) timestamp.textContent = 'Generated on: ' + new Date().toLocaleString();

    if (label) {
        if (start && end) {
            label.textContent = `Period: ${start} ~ ${end}`;
        } else {
            label.textContent = 'Period: Custom range';
        }
    }
}

// ====== Filter logic ======
document.addEventListener('DOMContentLoaded', () => {
    initReportFilters();
    generateReport(); // default view
});

function initReportFilters() {
    const form = document.getElementById('reportFilters');
    if (!form) return;

    const rangeSelect = document.getElementById('dateRange');
    const startInput = document.getElementById('startDate');
    const endInput = document.getElementById('endDate');

    form.addEventListener('submit', (e) => {
        e.preventDefault();
        generateReport();
    });

    form.addEventListener('reset', () => {
        setTimeout(() => {
            rangeSelect.value = 'month';
            applyDateRange('month', startInput, endInput);
            generateReport();
        }, 0);
    });

    rangeSelect.addEventListener('change', () => {
        applyDateRange(rangeSelect.value, startInput, endInput);
    });

    applyDateRange(rangeSelect.value, startInput, endInput);
}

function applyDateRange(rangeValue, startInput, endInput) {
    const today = new Date();
    let start = new Date(today);
    let end = new Date(today);
    let disableDates = true;

    switch (rangeValue) {
        case 'today':
            break;
        case 'week':
            start.setDate(today.getDate() - 6);
            break;
        case 'month':
            start = new Date(today.getFullYear(), today.getMonth(), 1);
            end = new Date(today.getFullYear(), today.getMonth() + 1, 0);
            break;
        case 'quarter':
            const currentQuarter = Math.floor(today.getMonth() / 3);
            start = new Date(today.getFullYear(), currentQuarter * 3, 1);
            end = new Date(today.getFullYear(), currentQuarter * 3 + 3, 0);
            break;
        case 'year':
            start = new Date(today.getFullYear(), 0, 1);
            end = new Date(today.getFullYear(), 11, 31);
            break;
        case 'custom':
            disableDates = false;
            start = null;
            end = null;
            break;
    }

    if (disableDates) {
        startInput.value = formatDateInput(start);
        endInput.value = formatDateInput(end);
        startInput.disabled = true;
        endInput.disabled = true;
    } else {
        startInput.value = '';
        endInput.value = '';
        startInput.disabled = false;
        endInput.disabled = false;
    }
}

function formatDateInput(date) {
    if (!date) return '';
    const month = (date.getMonth() + 1).toString().padStart(2, '0');
    const day = date.getDate().toString().padStart(2, '0');
    return `${date.getFullYear()}-${month}-${day}`;
}

// ====== Main generateReport() ======
function generateReport() {
    const type = document.getElementById('reportType')?.value || 'revenue';
    const range = document.getElementById('dateRange')?.value || 'month';
    const start = document.getElementById('startDate')?.value;  // '2024-12-01'
    const end = document.getElementById('endDate')?.value;      // '2024-12-31'

    const config = reportData[type] || reportData.revenue;

    // 更新标题 / 日期区
    updateReportHeader(config, range, start, end);
    renderSummaryCards(config.summary);

    // ===== 关键：根据日期过滤行 =====
    let filteredRows = config.rows;

    if (start || end) {
        const startDate = start ? new Date(start) : null;
        const endDate = end ? new Date(end) : null;

        filteredRows = config.rows.filter(row => {
            // 假设第 1 列是日期（如 '2024-12-01' 或 '2024-11-30'）
            const rowDate = new Date(row[0]);
            if (isNaN(rowDate)) {
                // 解析不了日期就直接保留（例如 services 报表没有日期）
                return true;
            }

            if (startDate && rowDate < startDate) return false;
            if (endDate && rowDate > endDate) return false;
            return true;
        });
    }

    const container = document.getElementById('reportTableContainer');
    if (container) {
        container.innerHTML = buildTable(config.columns, filteredRows, type);
    }
}

// ====== Export & Print ======
function printReport() {
    window.print();
}

function downloadExcel() {
    const table = document.querySelector('#reportTableContainer table');
    if (!table) {
        alert('No report table to export.');
        return;
    }

    const rows = table.querySelectorAll('tr');
    const csv = Array.from(rows).map(row => {
        return Array.from(row.querySelectorAll('th, td')).map(cell => {
            const text = cell.innerText.replace(/(\r\n|\n|\r)/gm, ' ').replace(/,/g, ' ');
            return `"${text.trim()}"`;
        }).join(',');
    }).join('\n');

   const blob = new Blob([csv], { type: 'text/csv;charset=utf-8;' });

const typeSelect = document.getElementById('reportType');
const reportType = typeSelect ? typeSelect.value : 'report';
const fileName = reportType + '_' + new Date().toISOString().slice(0, 10) + '.csv';

if (navigator.msSaveBlob) {
    navigator.msSaveBlob(blob, fileName); // IE 10+
} else {
    const link = document.createElement('a');
    if (link.download !== undefined) {
        const url = URL.createObjectURL(blob);
        link.setAttribute('href', url);
        link.setAttribute('download', fileName);
        link.style.visibility = 'hidden';
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
    }
}
}

function downloadPdf() {
    var report = document.getElementById('reportArea');
    if (!report) {
        alert('No report content to export.');
        return;
    }

    var typeSelect = document.getElementById('reportType');
    var reportType = typeSelect ? typeSelect.value : 'report';
    var fileName = reportType + '_' + new Date().toISOString().slice(0, 10) + '.pdf';

    var opt = {
        margin:       0.5,                // 英寸，可按需要调小/调大
        filename:     fileName,
        image:        { type: 'jpeg', quality: 0.95 },
        html2canvas:  { scale: 2, useCORS: true },
        jsPDF:        { unit: 'in', format: 'a4', orientation: 'portrait' }
    };

    html2pdf().set(opt).from(report).save();
}

function downloadPdf() {
    var report = document.getElementById('reportArea');
    if (!report) {
        alert('No report content to export.');
        return;
    }

    var typeSelect = document.getElementById('reportType');
    var reportType = typeSelect ? typeSelect.value : 'report';
    var fileName = reportType + '_' + new Date().toISOString().slice(0, 10) + '.pdf';

    var opt = {
        margin: 0.5,                // 英寸，可按需要调小/调大
        filename: fileName,
        image: { type: 'jpeg', quality: 0.95 },
        html2canvas: { scale: 2, useCORS: true },
        jsPDF: { unit: 'in', format: 'a4', orientation: 'portrait' }
    };

    html2pdf().set(opt).from(report).save();
}

// ========================================
// ALERT AUTO-HIDE FUNCTIONALITY
// ========================================
function initAlertAutoHide() {
    const alerts = document.querySelectorAll('.alert');
    alerts.forEach(alert => {
        setTimeout(() => {
            alert.style.transition = 'opacity 0.5s ease';
            alert.style.opacity = '0';
            setTimeout(() => alert.remove(), 500);
        }, 5000);
    });
}

// ========================================
// INITIALIZE ON DOM LOAD
// ========================================
document.addEventListener('DOMContentLoaded', function () {
    // Initialize existing modules
    if (typeof CustomerManager !== 'undefined' && CustomerManager.init) {
        CustomerManager.init();
    }
    if (typeof PetManager !== 'undefined' && PetManager.init) {
        PetManager.init();
    }
    if (typeof LoyaltyPointsManager !== 'undefined' && LoyaltyPointsManager.init) {
        LoyaltyPointsManager.init();
    }

    // Initialize alert auto-hide
    initAlertAutoHide();

    // Initialize report filters if on reports page
    if (document.getElementById('reportFilters')) {
        initReportFilters();
        generateReport();
    }
});