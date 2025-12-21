/**
 * Dark Mode Manager
 * Handles dark mode toggle and persistence across all pages
 */
class DarkModeManager {
    constructor() {
        this.storageKey = 'harmi-dark-mode';
        this.darkModeClass = 'dark-mode';
        this.init();
    }

    init() {
        // Load saved preference or check system preference
        const savedMode = localStorage.getItem(this.storageKey);
        
        if (savedMode !== null) {
            // Use saved preference
            this.setDarkMode(savedMode === 'true');
        } else {
            // Check system preference
            const prefersDark = window.matchMedia('(prefers-color-scheme: dark)').matches;
            this.setDarkMode(prefersDark);
        }

        // Listen for system theme changes
        window.matchMedia('(prefers-color-scheme: dark)').addEventListener('change', (e) => {
            if (localStorage.getItem(this.storageKey) === null) {
                this.setDarkMode(e.matches);
            }
        });

        // Create and inject toggle button
        this.createToggleButton();
    }

    /**
     * Create and inject the toggle button
     */
    createToggleButton() {
        // Check if button already exists
        if (document.getElementById('darkModeToggle')) {
            this.setupToggleListener();
            return;
        }

        const button = document.createElement('button');
        button.id = 'darkModeToggle';
        button.className = 'dark-mode-toggle';
        button.setAttribute('title', 'Toggle dark mode');
        button.setAttribute('aria-label', 'Toggle dark mode');

        const icon = document.createElement('span');
        icon.id = 'darkModeIcon';
        icon.textContent = this.isDarkMode() ? '☀️' : '🌙';

        button.appendChild(icon);
        document.body.appendChild(button);

        this.setupToggleListener();
    }

    /**
     * Setup toggle button click listener
     */
    setupToggleListener() {
        const toggleBtn = document.getElementById('darkModeToggle');
        const icon = document.getElementById('darkModeIcon');

        if (!toggleBtn) return;

        // Remove any existing listeners by cloning
        const newBtn = toggleBtn.cloneNode(true);
        toggleBtn.parentNode.replaceChild(newBtn, toggleBtn);

        newBtn.addEventListener('click', () => {
            this.toggle();
            this.updateIcon();
        });
    }

    /**
     * Update toggle button icon
     */
    updateIcon() {
        const icon = document.getElementById('darkModeIcon');
        if (icon) {
            const isDark = this.isDarkMode();
            icon.textContent = isDark ? '☀️' : '🌙';
        }
    }

    /**
     * Set dark mode on/off
     * @param {boolean} isDark - True for dark mode, false for light mode
     */
    setDarkMode(isDark) {
        const html = document.documentElement;
        
        if (isDark) {
            html.classList.add(this.darkModeClass);
        } else {
            html.classList.remove(this.darkModeClass);
        }

        // Save preference
        localStorage.setItem(this.storageKey, isDark ? 'true' : 'false');
    }

    /**
     * Toggle dark mode
     */
    toggle() {
        const isDark = document.documentElement.classList.contains(this.darkModeClass);
        this.setDarkMode(!isDark);
    }

    /**
     * Check if dark mode is currently active
     * @returns {boolean}
     */
    isDarkMode() {
        return document.documentElement.classList.contains(this.darkModeClass);
    }
}

// Initialize dark mode when DOM is ready
if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', () => {
        window.darkModeManager = new DarkModeManager();
    });
} else {
    window.darkModeManager = new DarkModeManager();
}

// Also reinitialize on page transitions (for SPA behavior)
window.addEventListener('load', () => {
    if (window.darkModeManager) {
        window.darkModeManager.createToggleButton();
    }
});