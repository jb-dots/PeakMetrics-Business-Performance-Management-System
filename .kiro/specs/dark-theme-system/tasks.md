# Dark Theme System - Implementation Tasks

## Phase 1: Foundation Setup

### Task 1.1: Create CSS Variables File
- [ ] Create `wwwroot/css/theme.css`
- [ ] Define light theme CSS variables (default)
- [ ] Define dark theme CSS variables under `[data-theme="dark"]`
- [ ] Include all color variables: backgrounds, text, borders, shadows, links

### Task 1.2: Create Theme Manager JavaScript
- [ ] Create `wwwroot/js/theme.js`
- [ ] Implement `ThemeManager` object with methods:
  - [ ] `init()` - Initialize theme system
  - [ ] `getTheme()` - Get current theme from localStorage
  - [ ] `setTheme(theme)` - Save and apply theme
  - [ ] `applyTheme(theme)` - Update DOM with theme
  - [ ] `toggleTheme()` - Switch between light/dark
  - [ ] `setupToggles()` - Attach event listeners
  - [ ] `updateToggleIcons(theme)` - Update button icons
- [ ] Add DOMContentLoaded event listener to initialize

### Task 1.3: Update Main Layout
- [ ] Open `Views/Shared/_Layout.cshtml`
- [ ] Add `data-theme="light"` attribute to `<body>` tag
- [ ] Add `<link>` tag for `theme.css` in `<head>`
- [ ] Add `<script>` tag for `theme.js` before closing `</body>`
- [ ] Add inline script to prevent FOUC (flash of unstyled content):
  ```html
  <script>
    (function() {
      const theme = localStorage.getItem('peakmetrics-theme') || 'light';
      document.documentElement.setAttribute('data-theme', theme);
    })();
  </script>
  ```

## Phase 2: Landing Page Implementation

### Task 2.1: Add Theme Toggle to Landing Page Navbar
- [ ] Open `Views/Landing/Index.cshtml`
- [ ] Add theme toggle button to navbar between nav links and Sign In button
- [ ] Add CSS styles for `.theme-toggle` button
- [ ] Add hover and focus states
- [ ] Test button appears correctly on desktop and mobile

### Task 2.2: Migrate Landing Page Styles to CSS Variables
- [ ] Update hero section background gradient to use variables
- [ ] Update navbar styles to use variables
- [ ] Update card backgrounds to use variables
- [ ] Update text colors to use variables
- [ ] Update feature section styles to use variables
- [ ] Update footer styles to use variables
- [ ] Test landing page in both light and dark themes

### Task 2.3: Test Landing Page Theme Switching
- [ ] Verify toggle button works
- [ ] Verify theme persists on page refresh
- [ ] Verify smooth transitions
- [ ] Verify no visual glitches
- [ ] Test on mobile viewport

## Phase 3: Main Application Implementation

### Task 3.1: Migrate site.css to CSS Variables
- [ ] Open `wwwroot/css/site.css`
- [ ] Replace hardcoded sidebar colors with variables
- [ ] Replace hardcoded navbar colors with variables
- [ ] Replace hardcoded card colors with variables
- [ ] Replace hardcoded text colors with variables
- [ ] Replace hardcoded input/form colors with variables
- [ ] Replace hardcoded button colors with variables
- [ ] Replace hardcoded table colors with variables

### Task 3.2: Add Theme Toggle to Profile Settings
- [ ] Open `Views/Home/Profile.cshtml`
- [ ] Add "Appearance" section to profile page
- [ ] Add theme toggle button with current theme display
- [ ] Add CSS styles for theme selector
- [ ] Test theme toggle in profile settings

### Task 3.3: Update Dashboard Views
- [ ] Update `Views/Home/_StaffDashboard.cshtml` to use CSS variables
- [ ] Update `Views/Home/_ManagerDashboard.cshtml` to use CSS variables
- [ ] Update `Views/Home/_ExecutiveDashboard.cshtml` to use CSS variables
- [ ] Update `Views/Home/_AdministratorDashboard.cshtml` to use CSS variables
- [ ] Test all dashboards in both themes

### Task 3.4: Update Management Pages
- [ ] Update `Views/Home/UserManagement.cshtml` to use CSS variables
- [ ] Update `Views/Home/DepartmentManagement.cshtml` to use CSS variables
- [ ] Update `Views/Home/KPITracking.cshtml` to use CSS variables
- [ ] Update `Views/Home/StrategicPlanning.cshtml` to use CSS variables
- [ ] Update `Views/Home/BalancedScorecard.cshtml` to use CSS variables
- [ ] Update `Views/Home/ExecutiveReporting.cshtml` to use CSS variables
- [ ] Test all pages in both themes

### Task 3.5: Update Form and Modal Styles
- [ ] Update form input styles to use CSS variables
- [ ] Update modal styles to use CSS variables
- [ ] Update button styles to use CSS variables
- [ ] Update toast notification styles to use CSS variables
- [ ] Test forms and modals in both themes

## Phase 4: Polish and Testing

### Task 4.1: Add Smooth Transitions
- [ ] Add CSS transitions for theme changes
- [ ] Add transition for background colors (0.3s ease)
- [ ] Add transition for text colors (0.3s ease)
- [ ] Add transition for border colors (0.3s ease)
- [ ] Test transitions feel smooth, not jarring

### Task 4.2: Prevent Flash of Unstyled Content (FOUC)
- [ ] Ensure inline script in `<head>` applies theme before page renders
- [ ] Test page load with saved dark theme preference
- [ ] Verify no flash of light theme before dark theme applies
- [ ] Test on slow network connections

### Task 4.3: Accessibility Testing
- [ ] Test keyboard navigation to theme toggle (Tab key)
- [ ] Test activating toggle with Enter/Space keys
- [ ] Verify ARIA labels are present and correct
- [ ] Test with screen reader (announce theme changes)
- [ ] Verify color contrast meets WCAG AA in both themes
- [ ] Verify focus indicators visible in both themes

### Task 4.4: Cross-Browser Testing
- [ ] Test in Chrome/Edge (Windows)
- [ ] Test in Firefox (Windows)
- [ ] Test in Safari (if available)
- [ ] Test on mobile Chrome (Android)
- [ ] Test on mobile Safari (iOS)
- [ ] Fix any browser-specific issues

### Task 4.5: Visual Consistency Check
- [ ] Review all pages in light theme for consistency
- [ ] Review all pages in dark theme for consistency
- [ ] Fix any color mismatches or visual glitches
- [ ] Ensure brand colors maintained in both themes
- [ ] Verify icons and images look good in both themes

## Phase 5: Documentation and Deployment

### Task 5.1: Update Documentation
- [ ] Document theme system in README or developer docs
- [ ] Document CSS variable naming convention
- [ ] Document how to add new themed components
- [ ] Add comments to theme.css explaining variable usage

### Task 5.2: Final Testing
- [ ] Complete end-to-end test of theme switching
- [ ] Test theme persistence across multiple sessions
- [ ] Test with cleared localStorage (defaults to light)
- [ ] Test rapid theme switching (no errors)
- [ ] Verify no console errors in browser

### Task 5.3: Deploy to Production
- [ ] Commit all changes to git
- [ ] Run `.\deploy.bat` to deploy to production
- [ ] Test on production site
- [ ] Monitor for any user-reported issues

## Optional Enhancements (Future)

### Task 6.1: System Preference Detection
- [ ] Add CSS media query for `prefers-color-scheme`
- [ ] Update ThemeManager to detect system preference
- [ ] Use system preference as default if no saved preference
- [ ] Add "Auto" option to theme selector

### Task 6.2: Theme Preview
- [ ] Add preview mode before applying theme
- [ ] Show sample of how theme will look
- [ ] Add "Apply" and "Cancel" buttons

### Task 6.3: Custom Theme Colors
- [ ] Allow users to customize accent colors
- [ ] Add color picker to profile settings
- [ ] Save custom colors to localStorage or database

## Notes

- **Login page stays light theme only** - No changes needed
- **Register page stays light theme only** - No changes needed
- **Default theme is light** - Matches current design
- **Theme preference is per-browser** - Uses localStorage, not database
- **All existing functionality must work in both themes**

## Estimated Time

- Phase 1: 2-3 hours
- Phase 2: 2-3 hours
- Phase 3: 4-6 hours
- Phase 4: 2-3 hours
- Phase 5: 1-2 hours

**Total: 11-17 hours**
