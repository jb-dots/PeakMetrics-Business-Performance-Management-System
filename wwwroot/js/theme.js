/**
 * PeakMetrics Theme Manager
 * Handles light/dark theme switching and persistence
 */

const ThemeManager = {
  STORAGE_KEY: 'peakmetrics-theme',
  THEME_ATTR: 'data-theme',
  
  /**
   * Initialize the theme system
   */
  init() {
    // Load saved theme or default to light
    const savedTheme = this.getTheme();
    this.applyTheme(savedTheme, true);
    
    // Set up toggle listeners
    this.setupToggles();
    
    // Remove no-transition class after initial load
    setTimeout(() => {
      document.body.classList.remove('no-transition');
    }, 100);
  },
  
  /**
   * Get current theme from localStorage
   * @returns {string} 'light' or 'dark'
   */
  getTheme() {
    return localStorage.getItem(this.STORAGE_KEY) || 'light';
  },
  
  /**
   * Save and apply theme
   * @param {string} theme - 'light' or 'dark'
   */
  setTheme(theme) {
    localStorage.setItem(this.STORAGE_KEY, theme);
    this.applyTheme(theme);
  },
  
  /**
   * Apply theme to DOM
   * @param {string} theme - 'light' or 'dark'
   * @param {boolean} skipTransition - Skip transition on initial load
   */
  applyTheme(theme, skipTransition = false) {
    if (skipTransition) {
      document.body.classList.add('no-transition');
    }
    
    document.documentElement.setAttribute(this.THEME_ATTR, theme);
    this.updateToggleIcons(theme);
    this.updateToggleText(theme);
  },
  
  /**
   * Toggle between light and dark theme
   */
  toggleTheme() {
    const currentTheme = this.getTheme();
    const newTheme = currentTheme === 'light' ? 'dark' : 'light';
    this.setTheme(newTheme);
  },
  
  /**
   * Set up event listeners for theme toggle buttons
   */
  setupToggles() {
    const toggles = document.querySelectorAll('[data-theme-toggle]');
    toggles.forEach(toggle => {
      toggle.addEventListener('click', (e) => {
        e.preventDefault();
        this.toggleTheme();
      });
    });
  },
  
  /**
   * Update toggle button icons based on current theme
   * @param {string} theme - 'light' or 'dark'
   */
  updateToggleIcons(theme) {
    const toggles = document.querySelectorAll('[data-theme-toggle]');
    toggles.forEach(toggle => {
      const icon = toggle.querySelector('i');
      if (icon) {
        // Light theme shows moon (switch to dark)
        // Dark theme shows sun (switch to light)
        icon.className = theme === 'light' ? 'bi bi-moon-fill' : 'bi bi-sun-fill';
      }
    });
  },
  
  /**
   * Update toggle button text based on current theme
   * @param {string} theme - 'light' or 'dark'
   */
  updateToggleText(theme) {
    const textElements = document.querySelectorAll('[data-theme-text]');
    textElements.forEach(el => {
      el.textContent = theme === 'light' ? 'Light' : 'Dark';
    });
  }
};

// Initialize theme before DOM loads to prevent flash
(function() {
  const theme = localStorage.getItem('peakmetrics-theme') || 'light';
  document.documentElement.setAttribute('data-theme', theme);
})();

// Initialize theme manager when DOM is ready
if (document.readyState === 'loading') {
  document.addEventListener('DOMContentLoaded', () => ThemeManager.init());
} else {
  ThemeManager.init();
}
