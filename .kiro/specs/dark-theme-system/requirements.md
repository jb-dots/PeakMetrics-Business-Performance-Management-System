# Dark Theme System - Requirements

## Overview
Implement a comprehensive dark/light theme system across the PeakMetrics application with user preference persistence.

## Scope

### Pages Affected
1. **Login Page** - Light theme only (no toggle)
2. **Landing Page** - Theme toggle in navbar
3. **Main Application** - Theme toggle in profile settings
4. **All Dashboard Views** - Support both themes

### User Stories
1. As a visitor, I want to toggle between light and dark theme on the landing page
2. As a logged-in user, I want to change my theme preference in profile settings
3. As a user, I want my theme preference to persist across sessions
4. As a user, I want smooth transitions when switching themes

## Requirements

### Functional Requirements
- FR1: Theme toggle button in landing page navbar
- FR2: Theme toggle in profile settings page
- FR3: Theme preference stored in localStorage
- FR4: Theme applied on page load based on saved preference
- FR5: Default theme is light
- FR6: Smooth CSS transitions between themes

### Technical Requirements
- TR1: Use CSS custom properties (variables) for theme colors
- TR2: JavaScript to handle theme switching
- TR3: localStorage key: `peakmetrics-theme` (values: `light` or `dark`)
- TR4: Add `data-theme` attribute to `<html>` or `<body>` tag
- TR5: Theme toggle icons: sun for light, moon for dark

### Design Requirements
- DR1: Dark theme uses dark blue gradient background
- DR2: Dark theme uses dark card backgrounds with light text
- DR3: Light theme uses current blue gradient with white cards
- DR4: Maintain brand colors and accessibility in both themes
- DR5: Toggle button should be subtle but discoverable

## Out of Scope
- Login page theme toggle (stays light only)
- Register page theme toggle
- System-level dark mode detection (may add later)
- Per-page theme preferences

## Success Criteria
1. Users can toggle theme on landing page
2. Users can toggle theme in profile settings
3. Theme preference persists across browser sessions
4. All pages render correctly in both themes
5. No flash of unstyled content on page load
