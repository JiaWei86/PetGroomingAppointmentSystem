class ChatbotManager {
    constructor() {
        this.container = document.getElementById('chatbot-container');
        this.toggleBtn = document.getElementById('chatbot-toggle');
        this.header = document.getElementById('chatbot-header');
        this.input = document.getElementById('chatbot-input');
        this.sendBtn = document.getElementById('chatbot-send');
        this.messages = document.getElementById('chatbot-messages');

        // Detect initial minimized state from DOM
        this.isMinimized = this.container.classList.contains('minimized');
        this.isLoading = false;

        this.init();
    }

    init() {
        this.updateToggleText();
        this.attachEventListeners();
    }

    attachEventListeners() {
        if (this.sendBtn) {
            this.sendBtn.addEventListener('click', () => this.sendMessage());
        }

        if (this.input) {
            this.input.addEventListener('keypress', (e) => {
                if (e.key === 'Enter' && !this.isLoading) this.sendMessage();
            });
        }

        // Only one toggle handler for BOTH header and toggle button
        if (this.header) {
            this.header.addEventListener('click', () => this.toggleMinimize());
        }
        if (this.toggleBtn) {
            this.toggleBtn.addEventListener('click', (e) => {
                e.stopPropagation();
                this.toggleMinimize();
            });
        }
    }

    toggleMinimize() {
        this.isMinimized = !this.isMinimized;
        if (this.isMinimized) {
            this.container.classList.add('minimized');
        } else {
            this.container.classList.remove('minimized');
        }
        this.updateToggleText();
    }

    updateToggleText() {
        if (this.isMinimized) {
            this.toggleBtn.textContent = '+';
        } else {
            this.toggleBtn.textContent = '−';
        }
    }

    async sendMessage() {
        const message = this.input?.value?.trim();
        if (!message || this.isLoading) return;

        this.addMessage(message, 'user');
        this.input.value = '';
        this.input.focus();

        this.showLoadingIndicator();
        this.isLoading = true;

        try {
            const response = await fetch('/api/chatbot/message', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ message })
            });

            this.removeLoadingIndicator();

            if (response.ok) {
                const data = await response.json();
                this.addMessage(data.response, 'bot');
            } else {
                const errorData = await response.json().catch(() => ({}));
                this.addMessage(errorData.error || 'Error occurred', 'bot');
            }
        } catch (e) {
            console.error(e);
            this.removeLoadingIndicator();
            this.addMessage("Service unavailable. Please try later.", 'bot');
        } finally {
            this.isLoading = false;
        }
    }

    addMessage(text, sender) {
        const greeting = this.messages.querySelector('.greeting-container');
        if (greeting && sender === 'user') greeting.remove();

        const msgDiv = document.createElement('div');
        msgDiv.className = `message ${sender}-message`;
        const p = document.createElement('p');
        p.textContent = text;
        msgDiv.appendChild(p);
        this.messages.appendChild(msgDiv);
        this.messages.scrollTop = this.messages.scrollHeight;
    }

    showLoadingIndicator() {
        const loadingDiv = document.createElement('div');
        loadingDiv.className = 'message bot-message';
        loadingDiv.id = 'loading-indicator';
        loadingDiv.innerHTML = `
            <div class="loading-indicator">
                <div class="loading-dot"></div>
                <div class="loading-dot"></div>
                <div class="loading-dot"></div>
            </div>`;
        this.messages.appendChild(loadingDiv);
        this.messages.scrollTop = this.messages.scrollHeight;
    }

    removeLoadingIndicator() {
        const el = document.getElementById('loading-indicator');
        if (el) el.remove();
    }
}


