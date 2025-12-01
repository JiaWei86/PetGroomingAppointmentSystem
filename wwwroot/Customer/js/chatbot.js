class ChatbotManager {
    constructor() {
        this.isMinimized = false;
        this.isLoading = false;
        this.init();
    }

    init() {
        this.injectChatbotUI();
        this.injectChatbotStyles();
        this.attachEventListeners();
    }

    injectChatbotUI() {
        const chatbotHTML = `
            <div id="chatbot-container" class="chatbot-container">
                <div id="chatbot-header" class="chatbot-header">
                    <div class="chatbot-header-content">
                        <h5>🐾 Hajimi House Assistant</h5>
                    </div>
                    <button id="chatbot-toggle" class="chatbot-toggle" title="Minimize">−</button>
                </div>
                
                <div id="chatbot-messages" class="chatbot-messages">
                    <div class="greeting-container">
                        <div class="greeting-header">
                            <h4>Hello! 👋</h4>
                            <p>I'm Hajimi's AI Assistant</p>
                        </div>
                        <div class="greeting-divider"></div>
                        <div class="greeting-content">
                            <p class="greeting-question">How can I help you today?</p>
                            <div class="greeting-topics">
                                <div class="topic-item">
                                    <span class="topic-emoji">🗓️</span>
                                    <span class="topic-text">Booking appointments</span>
                                </div>
                                <div class="topic-item">
                                    <span class="topic-emoji">💰</span>
                                    <span class="topic-text">Pricing information</span>
                                </div>
                                <div class="topic-item">
                                    <span class="topic-emoji">🐕</span>
                                    <span class="topic-text">Pet care advice</span>
                                </div>
                                <div class="topic-item">
                                    <span class="topic-emoji">📞</span>
                                    <span class="topic-text">Contact information</span>
                                </div>
                            </div>
                        </div>
                    </div>
                </div>
                
                <div class="chatbot-input-area">
                    <input 
                        type="text" 
                        id="chatbot-input" 
                        class="chatbot-input" 
                        placeholder="Type your question..." 
                        autocomplete="off">
                    <button id="chatbot-send" class="chatbot-send-btn" title="Send">
                        <i class="fa fa-paper-plane"></i>
                    </button>
                </div>
            </div>
        `;

        document.body.insertAdjacentHTML('beforeend', chatbotHTML);
    }

    injectChatbotStyles() {
        const styles = `
            .chatbot-container {
                position: fixed;
                bottom: 20px;
                right: 20px;
                width: 380px;
                max-width: calc(100vw - 40px);
                height: 550px;
                background: #ffffff;
                border-radius: 12px;
                box-shadow: 0 5px 40px rgba(0, 0, 0, 0.15);
                display: flex;
                flex-direction: column;
                z-index: 9999;
                font-family: "Open Sans", sans-serif;
                transition: all 0.3s ease;
            }

            .chatbot-container.minimized {
                height: 50px;
            }

            .chatbot-container.minimized .chatbot-messages,
            .chatbot-container.minimized .chatbot-input-area {
                display: none;
            }

            .chatbot-header {
                background: linear-gradient(135deg, #ff3500 0%, #ff5733 100%);
                color: white;
                padding: 15px 20px;
                border-radius: 12px 12px 0 0;
                display: flex;
                justify-content: space-between;
                align-items: center;
                user-select: none;
                flex-shrink: 0;
            }

            .chatbot-container.minimized .chatbot-header {
                border-radius: 12px;
                cursor: pointer;
            }

            .chatbot-header-content {
                flex: 1;
            }

            .chatbot-header h5 {
                margin: 0;
                font-size: 16px;
                font-weight: 600;
                letter-spacing: 0.5px;
            }

            .chatbot-toggle {
                background: rgba(255, 255, 255, 0.3);
                border: none;
                color: white;
                cursor: pointer;
                font-size: 24px;
                padding: 0 8px;
                border-radius: 4px;
                transition: all 0.3s ease;
                display: flex;
                align-items: center;
                justify-content: center;
                width: 32px;
                height: 32px;
                line-height: 1;
            }

            .chatbot-toggle:hover {
                background: rgba(255, 255, 255, 0.5);
                transform: scale(1.1);
            }

            .chatbot-toggle:active {
                transform: scale(0.95);
            }

            .chatbot-messages {
                flex: 1;
                overflow-y: auto;
                padding: 20px 15px;
                display: flex;
                flex-direction: column;
                gap: 12px;
                scrollbar-width: thin;
                scrollbar-color: #ddd #f5f5f5;
            }

            .chatbot-messages::-webkit-scrollbar {
                width: 6px;
            }

            .chatbot-messages::-webkit-scrollbar-track {
                background: #f5f5f5;
                border-radius: 10px;
            }

            .chatbot-messages::-webkit-scrollbar-thumb {
                background: #ddd;
                border-radius: 10px;
            }

            .chatbot-messages::-webkit-scrollbar-thumb:hover {
                background: #bbb;
            }

            .greeting-container {
                display: flex;
                flex-direction: column;
                gap: 16px;
                animation: chatbotFadeIn 0.4s ease;
            }

            @keyframes chatbotFadeIn {
                from {
                    opacity: 0;
                    transform: translateY(10px);
                }
                to {
                    opacity: 1;
                    transform: translateY(0);
                }
            }

            .greeting-header {
                text-align: center;
            }

            .greeting-header h4 {
                margin: 0;
                font-size: 24px;
                font-weight: 600;
                color: #ff3500;
                letter-spacing: 0.5px;
            }

            .greeting-header p {
                margin: 8px 0 0 0;
                font-size: 14px;
                color: #666;
                font-weight: 500;
            }

            .greeting-divider {
                height: 2px;
                background: linear-gradient(90deg, transparent, #ff3500, transparent);
                border-radius: 1px;
            }

            .greeting-content {
                display: flex;
                flex-direction: column;
                gap: 12px;
            }

            .greeting-question {
                margin: 0;
                text-align: center;
                font-size: 14px;
                color: #333;
                font-weight: 500;
            }

            .greeting-topics {
                display: grid;
                grid-template-columns: 1fr;
                gap: 10px;
            }

            .topic-item {
                display: flex;
                align-items: center;
                gap: 10px;
                padding: 10px 12px;
                background: #f9f9f9;
                border-radius: 8px;
                border-left: 3px solid #ff3500;
                transition: all 0.3s ease;
                cursor: pointer;
            }

            .topic-item:hover {
                background: #f0f0f0;
                transform: translateX(4px);
            }

            .topic-emoji {
                font-size: 18px;
                min-width: 24px;
                text-align: center;
            }

            .topic-text {
                font-size: 13px;
                color: #333;
                font-weight: 500;
            }

            .message {
                display: flex;
                margin-bottom: 8px;
                animation: chatbotSlideIn 0.3s ease;
            }

            @keyframes chatbotSlideIn {
                from {
                    opacity: 0;
                    transform: translateY(10px);
                }
                to {
                    opacity: 1;
                    transform: translateY(0);
                }
            }

            .bot-message {
                justify-content: flex-start;
            }

            .bot-message p {
                background: #f0f0f0;
                color: #333;
                padding: 12px 15px;
                border-radius: 12px;
                margin: 0;
                max-width: 85%;
                word-wrap: break-word;
                font-size: 13px;
                line-height: 1.5;
                box-shadow: 0 1px 3px rgba(0, 0, 0, 0.1);
            }

            .user-message {
                justify-content: flex-end;
            }

            .user-message p {
                background: linear-gradient(135deg, #ff3500 0%, #ff5733 100%);
                color: white;
                padding: 12px 15px;
                border-radius: 12px;
                margin: 0;
                max-width: 85%;
                word-wrap: break-word;
                font-size: 13px;
                line-height: 1.5;
                box-shadow: 0 1px 3px rgba(255, 53, 0, 0.3);
            }

            .chatbot-input-area {
                display: flex;
                gap: 8px;
                padding: 12px;
                border-top: 1px solid #eee;
                background: #fafafa;
                flex-shrink: 0;
            }

            .chatbot-input {
                flex: 1;
                border: 1px solid #ddd;
                border-radius: 6px;
                padding: 10px 12px;
                font-size: 13px;
                font-family: inherit;
                outline: none;
                transition: all 0.3s ease;
                background: white;
            }

            .chatbot-input:focus {
                border-color: #ff3500;
                box-shadow: 0 0 0 2px rgba(255, 53, 0, 0.1);
            }

            .chatbot-input::placeholder {
                color: #999;
            }

            .chatbot-send-btn {
                background: linear-gradient(135deg, #ff3500 0%, #ff5733 100%);
                color: white;
                border: none;
                border-radius: 6px;
                padding: 10px 15px;
                cursor: pointer;
                font-size: 14px;
                transition: all 0.2s ease;
                display: flex;
                align-items: center;
                justify-content: center;
                min-width: 44px;
                height: 44px;
            }

            .chatbot-send-btn:hover {
                opacity: 0.9;
                transform: scale(1.05);
                box-shadow: 0 2px 8px rgba(255, 53, 0, 0.3);
            }

            .chatbot-send-btn:active {
                transform: scale(0.95);
            }

            .chatbot-send-btn:disabled {
                opacity: 0.6;
                cursor: not-allowed;
            }

            .loading-indicator {
                display: flex;
                align-items: center;
                gap: 6px;
                padding: 12px 15px;
                background: #f0f0f0;
                border-radius: 12px;
                width: fit-content;
            }

            .loading-dot {
                width: 8px;
                height: 8px;
                background: #ff3500;
                border-radius: 50%;
                animation: chatbotPulse 1.4s infinite;
            }

            .loading-dot:nth-child(2) {
                animation-delay: 0.2s;
            }

            .loading-dot:nth-child(3) {
                animation-delay: 0.4s;
            }

            @keyframes chatbotPulse {
                0%, 60%, 100% {
                    opacity: 0.3;
                }
                30% {
                    opacity: 1;
                }
            }

            @media (max-width: 480px) {
                .chatbot-container {
                    width: calc(100vw - 20px);
                    height: 70vh;
                    bottom: 10px;
                    right: 10px;
                }
                
                .bot-message p,
                .user-message p {
                    max-width: 90%;
                    font-size: 12px;
                }

                .greeting-topics {
                    grid-template-columns: 1fr;
                }
            }
        `;

        const styleElement = document.createElement('style');
        styleElement.id = 'chatbot-styles';
        styleElement.textContent = styles;
        document.head.appendChild(styleElement);
    }

    attachEventListeners() {
        const input = document.getElementById('chatbot-input');
        const sendBtn = document.getElementById('chatbot-send');
        const toggleBtn = document.getElementById('chatbot-toggle');
        const header = document.querySelector('.chatbot-header');

        if (sendBtn) {
            sendBtn.addEventListener('click', () => this.sendMessage());
        }

        if (input) {
            input.addEventListener('keypress', (e) => {
                if (e.key === 'Enter' && !this.isLoading) {
                    this.sendMessage();
                }
            });
        }

        if (toggleBtn) {
            toggleBtn.addEventListener('click', (e) => {
                e.stopPropagation();
                this.toggleMinimize();
            });
        }

        if (header) {
            header.addEventListener('click', () => {
                if (this.isMinimized) {
                    this.toggleMinimize();
                }
            });
        }
    }

    async sendMessage() {
        const input = document.getElementById('chatbot-input');
        const message = input?.value?.trim();

        if (!message || this.isLoading) return;

        // Add user message to chat
        this.addMessage(message, 'user');
        input.value = '';
        input.focus();

        // Show loading indicator
        this.showLoadingIndicator();
        this.isLoading = true;

        try {
            const response = await fetch('/api/chatbot/message', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify({ message })
            });

            this.removeLoadingIndicator();

            if (response.ok) {
                const data = await response.json();
                this.addMessage(data.response, 'bot');
            } else {
                const errorData = await response.json().catch(() => ({}));
                const errorMessage = errorData.error || 'Sorry, I encountered an error. Please try again.';
                this.addMessage(errorMessage, 'bot');
            }
        } catch (error) {
            console.error('Chatbot error:', error);
            this.removeLoadingIndicator();
            this.addMessage('Sorry, I\'m temporarily unavailable. Please try again later.', 'bot');
        } finally {
            this.isLoading = false;
        }
    }

    addMessage(text, sender) {
        const messagesContainer = document.getElementById('chatbot-messages');
        
        // Remove greeting if it exists and this is the first user message
        const greeting = messagesContainer?.querySelector('.greeting-container');
        if (greeting && sender === 'user') {
            greeting.remove();
        }

        const messageDiv = document.createElement('div');
        messageDiv.className = `message ${sender}-message`;

        const textP = document.createElement('p');
        textP.textContent = text;
        messageDiv.appendChild(textP);

        messagesContainer?.appendChild(messageDiv);
        
        // Scroll to bottom
        if (messagesContainer) {
            setTimeout(() => {
                messagesContainer.scrollTop = messagesContainer.scrollHeight;
            }, 0);
        }
    }

    showLoadingIndicator() {
        const messagesContainer = document.getElementById('chatbot-messages');
        const loadingDiv = document.createElement('div');
        loadingDiv.className = 'message bot-message';
        loadingDiv.id = 'loading-indicator';
        loadingDiv.innerHTML = `
            <div class="loading-indicator">
                <div class="loading-dot"></div>
                <div class="loading-dot"></div>
                <div class="loading-dot"></div>
            </div>
        `;
        messagesContainer?.appendChild(loadingDiv);
        
        if (messagesContainer) {
            messagesContainer.scrollTop = messagesContainer.scrollHeight;
        }
    }

    removeLoadingIndicator() {
        const loadingDiv = document.getElementById('loading-indicator');
        if (loadingDiv) {
            loadingDiv.remove();
        }
    }

    toggleMinimize() {
        const container = document.getElementById('chatbot-container');
        const toggleBtn = document.getElementById('chatbot-toggle');

        this.isMinimized = !this.isMinimized;
        
        if (this.isMinimized) {
            container?.classList.add('minimized');
            if (toggleBtn) toggleBtn.textContent = '+';
        } else {
            container?.classList.remove('minimized');
            if (toggleBtn) toggleBtn.textContent = '−';
            
            // Scroll to bottom when maximized
            const messagesContainer = document.getElementById('chatbot-messages');
            if (messagesContainer) {
                setTimeout(() => {
                    messagesContainer.scrollTop = messagesContainer.scrollHeight;
                }, 100);
            }
        }
    }
}

// Initialize chatbot when DOM is ready
document.addEventListener('DOMContentLoaded', () => {
    new ChatbotManager();
});