# Accessibility

BillFoundry targets WCAG 2.2 AA. This document records a review of the
Community Edition UI. Prefer native semantics over ARIA.

## Areas reviewed

- Semantic HTML and page landmarks
- Heading hierarchy
- Skip navigation
- Form labels and validation
- Error announcements
- Keyboard navigation and visible focus
- Dialogs
- Tables
- Status and loading messages
- Accessible names
- Color-independent communication
- Contrast
- Responsive layout, zoom, and reflow
- Empty states
- Charts with equivalent tables

## Current behavior

The unauthenticated landing page uses the same skip-link and `main` pattern.

List pages use labeled filters, `aria-busy` while loading, polite result
counts, and visually hidden table captions. Status badges include text, not
color alone. Report bar charts are `aria-hidden` and are paired with data
tables.

Forms use visible `label` elements. Server errors use `role="alert"`.
Validation summaries now also use `role="alert"` so client-side validation is
announced.

The reconnect UI is a native `dialog` with an accessible name, `type="button"`
actions, and a visually hidden heading. The mobile navigation backdrop is a
button named "Close navigation"; Escape still closes the sidebar.

Dashboard loading uses `role="status"`. List empty states explain whether
filters hid rows or no records exist.

`:focus-visible` outlines are 3px on the accent color. The skip link becomes
visible on focus. `prefers-reduced-motion` is respected for the reconnect
animation.

## Changes made

- Named the reconnect dialog and gave Retry/Resume explicit button types
- Turned the mobile nav overlay into a keyboard-operable close control
- Announced validation summaries with `role="alert"`
- Marked dashboard loading as a status message

## Intentionally deferred

- Automated axe-core CI (manual review plus the tests above)
- Removing all inline bar-chart width styles (blocked by current CSS; the
  charts remain hidden from assistive technology)
- A dedicated high-contrast theme beyond `color-scheme: light`
- Full zoom/reflow lab measurements at 200% and 400% on every page

## Tests

Integration tests assert the login page exposes skip navigation, a main
landmark, a labeled heading, and a password field. Authenticated dashboard
tests assert skip navigation and the `h1`.
