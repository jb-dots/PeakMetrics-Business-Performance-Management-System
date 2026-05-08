# Dark Theme System - Design Document

## Architecture Overview

### Theme System Components
1. **CSS Variables** - Define all theme colors as CSS custom properties
2. **Theme Switcher Script** - JavaScript module to handle theme switching
3. **Theme Toggle UI** - Button components for landing page and profile settings
4. **Persistence Layer** - localStorage for saving user preference

### Data Flow
```
User clicks toggle → JS updates data-theme attribute → CSS variables change → UI updates
                  ↓
            Save to localStorage
```

## Technical Design

### 1. CSS Variables Structure

**File:** `wwwroot/css/theme.css` (new file)

```css
:root {
  /* Light theme (default) */
  --bg-gradient-start: #1e3a8a;
  --bg-gradient-mid: #2563eb;
  --bg-gradient-end: #60a5fa;
  
  --card-bg: #ffffff;
  --card-border: rgba(255, 255, 255, 0.1);
  --card-shadow: rgba(0, 0, 0, 0.1);
  
  --text-primary: #0f172a;
  --text-secondary: #64748b;
  --text-tertiary: #94a3b8;
  
  --input-border: #cbd5e1;
  --input-focus: #2563eb;
  --input-text: #0f172a;
  --input-placeholder: #94a3b8;
  
  --link-color: #2563eb;
  --link-hover: #1e40af;
  
  --sidebar-bg: #1e293b;
  --sidebar-text: #e2e8f0;
  --sidebar-hover: #334155;
  
  --navbar-bg: rgba(255, 255, 255, 0.92);
  --navbar-border: #e2e8f0;
  --navbar-text: #475569;
}

[data-theme="dark"] {
  /* Dark theme */
  --bg-gradient-start: #0a0e27;
  --bg-gradient-mid: #1a1f3a;
  --bg-gradient-end: #2563eb;
  
  --card-bg: #1a1f3a;
  --card-border: rgba(255, 255, 255, 0.05);
  --card-shadow: rgba(0, 0, 0, 0.5);
  
  --text-primary: #e2e8f0;
  --text-secondary: #94a3b8;
  --text-tertiary: #64748b;
  
  --input-border: #334155;
  --input-focus: #3b82f6;
  --input-text: #e2e8f0;
  --input-placeholder: #475569;
  
  --link-color: #60a5fa;
  --link-hover: #93c5fd;
  
  --sidebar-bg: #0f1729;
  --sidebar-text: #e2e8f0;
  --sidebar-hover: #1a2332;
  
  --navbar-bg: rgba(15, 23, 41, 0.92);
  --navbar-border: #1e293b;
  --navbar-text: #94a3b8;
}
```

### 2. Theme Switcher JavaScript

**File:** `wwwroot/js/theme.js` (new file)

```javascript
// Theme management
const ThemeManager = {
  STORAGE_KEY: 'peakmetrics-theme',
  THEME_ATTR: 'data-theme',
  
  init() {
    // Load saved theme or default to light
    const savedTheme = this.getTheme();
    this.applyTheme(savedTheme);
    
    // Set up toggle listeners
    this.setupToggles();
  },
  
  getTheme() {
    return localStorage.getItem(this.STORAGE_KEY) || 'light';
  },
  
  setTheme(theme) {
    localStorage.setItem(this.STORAGE_KEY, theme);
    this.applyTheme(theme);
  },
  
  applyTheme(theme) {
    document.documentElement.setAttribute(this.THEME_ATTR, theme);
    this.updateToggleIcons(theme);
  },
  
  toggleTheme() {
    const currentTheme = this.getTheme();
    const newTheme = currentTheme === 'light' ? 'dark' : 'light';
    this.setTheme(newTheme);
  },
  
  setupToggles() {
    const toggles = document.querySelectorAll('[data-theme-toggle]');
    toggles.forEach(toggle => {
      toggle.addEventListener('click', () => this.toggleTheme());
    });
  },
  
  updateToggleIcons(theme) {
    const toggles = document.querySelectorAll('[data-theme-toggle]');
    toggles.forEach(toggle => {
      const icon = toggle.querySelector('i');
      if (icon) {
        icon.className = theme === 'light' ? 'bi bi-moon-fill' : 'bi bi-sun-fill';
      }
    });
  }
};

// Initialize on DOM ready
document.addEventListener('DOMContentLoaded', () => {
  ThemeManager.init();
});
```

### 3. Landing Page Theme Toggle

**Location:** Navbar in `Views/Landing/Index.cshtml`

**HTML Structure:**
```html
<nav class="pm-navbar">
  <a href="/" class="pm-navbar-brand">...</a>
  <ul class="pm-navbar-links">
    <li><a href="#features">Features</a></li>
    <li><a href="#how-it-works">How It Works</a></li>
    <li><a href="#roles">Who It's For</a></li>
    <li>
      <button data-theme-toggle class="theme-toggle" aria-label="Toggle theme">
        <i class="bi bi-moon-fill"></i>
      </button>
    </li>
  </ul>
  <a href="/Home/Login" class="pm-btn-login">Sign In</a>
</nav>
```

**CSS for Toggle Button:**
```css
.theme-toggle {
  background: transparent;
  border: 2px solid var(--navbar-text);
  border-radius: 8px;
  width: 40px;
  height: 40px;
  display: flex;
  align-items: center;
  justify-content: center;
  cursor: pointer;
  transition: all 0.2s;
  color: var(--navbar-text);
}

.theme-toggle:hover {
  background: rgba(37, 99, 235, 0.1);
  border-color: #2563eb;
  color: #2563eb;
}

.theme-toggle i {
  font-size: 1.1rem;
}
```

### 4. Profile Settings Theme Toggle

**Location:** `Views/Home/Profile.cshtml`

**HTML Structure:**
```html
<div class="profile-section">
  <h3>Appearance</h3>
  <div class="theme-selector">
    <label>Theme</label>
    <div class="theme-options">
      <button data-theme-toggle class="theme-option">
        <i class="bi bi-moon-fill"></i>
        <span id="current-theme-text">Light</span>
      </button>
    </div>
  </div>
</div>
```

### 5. Main Layout Updates

**File:** `Views/Shared/_Layout.cshtml`

**Changes needed:**
1. Add theme.css link in `<head>`
2. Add theme.js script before closing `</body>`
3. Update existing CSS to use CSS variables
4. Add `data-theme="light"` to `<html>` or `<body>` tag

**Head section:**
```html
<link rel="stylesheet" href="~/css/theme.css" />
<link rel="stylesheet" href="~/css/site.css" />
```

**Body section (before closing tag):**
```html
<script src="~/js/theme.js"></script>
<script src="~/js/site.js"></script>
```

### 6. Existing CSS Migration

**Files to update:**
- `wwwroot/css/site.css` - Replace hardcoded colors with CSS variables
- `Views/Landing/Index.cshtml` - Update inline styles to use variables
- `Views/Shared/_Layout.cshtml` - Update sidebar and navbar styles

**Example migration:**
```css
/* Before */
.sidebar {
  background: #1e293b;
  color: #e2e8f0;
}

/* After */
.sidebar {
  background: var(--sidebar-bg);
  color: var(--sidebar-text);
}
```

## UI/UX Design

### Theme Toggle Button States

**Light Theme:**
- Icon: Moon (🌙)
- Tooltip: "Switch to dark mode"
- Border: Gray
- Hover: Blue tint

**Dark Theme:**
- Icon: Sun (☀️)
- Tooltip: "Switch to light mode"
- Border: Light gray
- Hover: Blue tint

### Transition Behavior
- Smooth CSS transitions (0.3s ease)
- No flash of unstyled content
- Immediate visual feedback on click

### Accessibility
- ARIA labels on toggle buttons
- Keyboard accessible (Tab + Enter)
- Sufficient color contrast in both themes
- Focus indicators visible in both themes

## Implementation Phases

### Phase 1: Foundation
1. Create theme.css with CSS variables
2. Create theme.js with ThemeManager
3. Update _Layout.cshtml to include new files

### Phase 2: Landing Page
1. Add theme toggle to navbar
2. Update landing page styles to use CSS variables
3. Test theme switching on landing page

### Phase 3: Main Application
1. Update site.css to use CSS variables
2. Add theme toggle to profile settings
3. Update all dashboard views to use CSS variables
4. Test theme switching across all pages

### Phase 4: Polish
1. Add smooth transitions
2. Test accessibility
3. Fix any visual inconsistencies
4. Add loading state handling (prevent FOUC)

## Testing Strategy

### Manual Testing
- [ ] Theme toggle works on landing page
- [ ] Theme toggle works in profile settings
- [ ] Theme persists across page refreshes
- [ ] Theme persists across browser sessions
- [ ] All pages render correctly in light theme
- [ ] All pages render correctly in dark theme
- [ ] No flash of unstyled content on page load
- [ ] Smooth transitions between themes
- [ ] Toggle button icons update correctly

### Browser Testing
- [ ] Chrome/Edge
- [ ] Firefox
- [ ] Safari
- [ ] Mobile browsers

### Accessibility Testing
- [ ] Keyboard navigation works
- [ ] Screen reader announces theme changes
- [ ] Color contrast meets WCAG AA standards
- [ ] Focus indicators visible in both themes

## Rollback Plan

If issues arise:
1. Remove theme.js and theme.css includes from _Layout.cshtml
2. Revert CSS variable changes in site.css
3. Remove theme toggle buttons
4. Application returns to light theme only

## Future Enhancements

- System preference detection (prefers-color-scheme)
- Per-page theme preferences
- Custom theme colors
- Theme preview before applying
- Scheduled theme switching (auto dark at night)
