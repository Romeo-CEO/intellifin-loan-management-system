# LMS UX/UI Style Guide (Design Brief)

This design brief sets the visual language and interaction principles for the LMS back-office platform. It prioritizes bold simplicity, clarity under operational pressure, and performance on low-bandwidth connections and tablets.

---

## Color Palette

### Primary Colors
- Primary Deep Teal — #0B6B62
  - Primary brand color for actions (primary buttons), highlights, and key icons
- Primary Ink — #0D1B2A
  - For high-contrast headings, nav text, and key affordances on light background

### Secondary Colors
- Secondary Slate — #334155
  - Secondary text, table headers, subdued icons
- Secondary Blue — #2563EB
  - Links, informational actions, secondary emphasis
- Secondary Soft Teal — #14B8A6
  - Hovers, focus rings, selected states, chips

### Accent Colors
- Accent Gold — #F59E0B
  - Attention-grabbing highlights (e.g., SLA timers), badges
- Accent Purple — #7C3AED
  - Data viz contrast series, selections in analytics

### Functional Colors
- Success Green — #16A34A
  - Success states, confirmations, positive deltas
- Warning Amber — #D97706
  - Warnings, pending attention, soft errors
- Error Red — #DC2626
  - Errors, destructive actions, validation failures
- Info Blue — #0EA5E9
  - Informational states, neutral alerts
- Neutral Gray — #6B7280
  - Secondary text, disabled controls
- Border/Subtle — #E5E7EB
  - Dividers, subtle control borders

### Background Colors
- Background Base — #F8FAFC
  - App canvas and large surfaces
- Surface — #FFFFFF
  - Cards, panels, tables, forms
- Surface Subtle — #F1F5F9
  - Alternate rows, empty states, selected zones
- Scrim/Overlay — rgba(15, 23, 42, 0.6)
  - Modals, drawers

### Gradients (used sparingly)
- Teal Gradient — linear-gradient(135deg, #0B6B62 0%, #14B8A6 100%)
  - Hero headers, KPI accent bars, not for large content blocks

---

## Typography

### Font Families
- Primary: Inter, "SF Pro Text", Roboto, "Segoe UI", system-ui, -apple-system, Arial, sans-serif
- Monospace (code/figures where needed): JetBrains Mono, Menlo, Consolas, monospace

### Font Weights
- Regular: 400
- Medium: 500
- Semibold: 600
- Bold: 700

### Text Styles
- H1: 28px / 36px, 700, letter-spacing -0.2px
  - Screen titles, page headers
- H2: 24px / 32px, 700, letter-spacing -0.2px
  - Section headers, card group headers
- H3: 20px / 28px, 600, letter-spacing -0.1px
  - Subsection headers, dialog titles
- Body Large: 17px / 26px, 400
  - Primary reading text, detail panes
- Body: 15px / 22px, 400
  - General UI text, tables
- Body Small: 13px / 18px, 400
  - Secondary text, helper copy
- Caption: 12px / 16px, 500, letter-spacing 0.2px
  - Timestamps, metadata, labels
- Button Text: 16px / 24px, 500, letter-spacing 0.1px
  - Buttons, interactive chips
- Link: 15px / 20px, 500, color Secondary Blue (#2563EB), underline on hover

### Typographic Principles
- Hierarchy via weight and size; keep headings 1–2 steps above body
- Line-length 60–80 characters for readability
- Use content-first phrasing; keep microcopy concise and actionable

---

## Component Styling

### Buttons
- Primary Button
  - Background: Primary Deep Teal (#0B6B62)
  - Text: White (#FFFFFF)
  - Height: 48dp (tablet), 44dp (desktop small)
  - Radius: 8dp
  - Padding: 16dp horizontal
  - Hover: Darken to #095D55; elevation +2 or border-highlight #14B8A6
  - Focus: 2dp focus ring #14B8A6 (outside)
  - Disabled: Background #94A3B8, Text #E2E8F0
- Secondary Button (Outlined)
  - Border: 1.5dp Primary Deep Teal (#0B6B62)
  - Text: Primary Deep Teal (#0B6B62)
  - Background: Transparent
  - Height: 48dp, Radius: 8dp
  - Hover: Surface Subtle background
- Tertiary / Text Button
  - Text: Secondary Blue (#2563EB)
  - No background, min-height 44dp
  - Hover: underline
- Destructive Button
  - Background: Error Red (#DC2626), Text: #FFFFFF
  - Hover: #B91C1C; Focus ring: #FCA5A5
- Icon Button
  - Shape: 40dp square, radius 8dp
  - Hover: Surface Subtle; Focus: teal ring

### Cards
- Background: Surface (#FFFFFF)
- Border: 1dp #E5E7EB or shadow (Y=2, Blur=8, Opacity 10%)
- Radius: 12dp
- Padding: 16–24dp
- Header: H3 + optional caption; divider #E5E7EB if needed
- States: default, selected (teal 2dp left bar), warning (amber left bar), error (red left bar)

### Input Fields
- Height: 56dp
- Radius: 8dp
- Background: Surface (#FFFFFF)
- Border: 1dp #E5E7EB
- Active/Focus Border: 2dp Secondary Soft Teal (#14B8A6)
- Text: #0D1B2A; Placeholder: #9CA3AF
- Helper/Error: 12px; error color Error Red (#DC2626)
- Icons: 20dp gray (#6B7280), turn teal on focus
- Variants: single-line, multi-line, with prefix/suffix, dropdown select, datepicker

### Tables
- Row height: 48dp (dense 40dp)
- Header: Secondary Slate (#334155), 600, background Surface Subtle (#F1F5F9)
- Gridlines: #E5E7EB; zebra rows optional (#F8FAFC)
- Interactions: sortable headers (icon on hover), sticky header, sticky first column
- States: loading skeleton, empty state with CTA, error with retry

### Badges & Status Chips
- Success: bg #DCFCE7 / text #166534
- Warning: bg #FEF3C7 / text #92400E
- Error: bg #FEE2E2 / text #991B1B
- Info: bg #E0F2FE / text #075985
- Neutral: bg #F1F5F9 / text #334155
- Shape: 18–22dp height, radius 999dp, 12–13px text

### Modals & Drawers
- Modal surface: #FFFFFF; radius 12dp; padding 24–32dp; shadow Y=16, Blur=32, 20%
- Drawer width: 420–520px; scrim rgba(15, 23, 42, 0.6)
- Close affordance: top-right icon button, 40dp target size

### Toasts & Inline Feedback
- Success/Warning/Error/Info maps to functional colors; 4–6s auto-dismiss with pause on hover
- Inline validation appears below field; avoid blocking modals unless destructive

### Icons
- Set: Fluent/Material Symbols rounded (consistent stroke weight) or Phosphor
- Sizes: 16dp (inline), 20dp (inputs), 24dp (general), 28dp (nav)
- Color: interactive icons use Primary Deep Teal (#0B6B62); inactive icons Neutral Gray (#6B7280)
- Accessibility: provide aria-labels; avoid meaning by color alone

---

## Spacing System
- 4dp — micro gaps between related elements
- 8dp — small padding inside components
- 12dp — compact gutter for dense UIs
- 16dp — default spacing
- 20dp — between stacked inputs
- 24dp — between sections within a card
- 32dp — section separators on pages
- 40dp — large layout spacing / page gutters (tablet)
- 48dp — top/bottom page padding; large gaps around major sections

### Layout Grid & Breakpoints
- Grid: 12-column fluid; gutter 24dp (desktop), 16dp (tablet)
- Sidebar: 264px; Top bar: 64px; Content max width: 1440px
- Breakpoints: tablet ≥ 768px, desktop ≥ 1024px, wide ≥ 1440px

---

## Motion & Animation
- Standard Transition: 200ms, ease-out (cubic-bezier(0.2, 0.8, 0.2, 1))
- Emphasis Transition (dialogs, major nav): 280–320ms, spring (tension 300, friction 35)
- Microinteractions (hover, toggles): 120–160ms, ease-in-out
- Page Transitions: 320–360ms, subtle slide/opacity for spatial continuity
- Skeletons: appear instantly, shimmer 1200ms linear
- Performance: prefer transform/opacity; cap simultaneous animations; reduce motion setting respected

---

## Dark Mode Variants
- Dark Background: #0B1220
- Dark Surface: #111827
- Dark Surface Subtle: #0F172A
- Dark Text Primary: #E5E7EB
- Dark Text Secondary: #9CA3AF
- Dark Primary Teal: #23C3B5 (contrast-adjusted)
- Dark Borders: #1F2937
- Dark Focus Ring: #23C3B5 @ 2dp
- Elevation: reduce shadows; use borders and subtle inner highlights

---

## Data Visualization
- Series Palette (colorblind-friendly)
  - Blue #2563EB, Teal #0EA5A4, Green #16A34A, Amber #F59E0B, Purple #7C3AED, Rose #E11D48, Slate #64748B, Emerald #059669
- Usage
  - KPIs: Primary Deep Teal for positive, Error Red for negative
  - Lines/bars: max 6 concurrent colors; emphasize current selection with thicker stroke
  - Tooltips: 13px, compact, with timestamp and source hint
  - Axes/Grids: #CBD5E1; labels #475569

---

## Accessibility
- Contrast: AA minimum (4.5:1 body, 3:1 large text). Verify buttons on Background Base.
- Focus: visible focus ring on all interactive elements, 2dp outer ring in Soft Teal.
- Targets: minimum 40dp touch targets on tablet.
- Semantics: headings in order; landmarks for nav/main/aside; labels tied to inputs.
- Motion: respect ‘prefers-reduced-motion’; provide non-animated alternatives.
- Color: never communicate status by color alone; pair color with icon/label.

---

## Content & Microcopy
- Tone: professional, concise, directive (“Upload payslip”, “Resolve variance”).
- Validation: say what and how to fix (“Amount exceeds cap. Reduce to ≤ ZMW 12,000 or request override”).
- Empty States: explain purpose + primary CTA + link to docs.

---

## Examples (Specs Summary)
- Primary Button: 48dp, radius 8dp, #0B6B62 bg, #FFFFFF text, hover darken, focus ring #14B8A6
- Card: #FFFFFF, radius 12dp, shadow (Y2 B8 O10%), padding 24dp, optional left bar for state
- Input: 56dp, border #E5E7EB → 2dp #14B8A6 on focus, helper 12px, error #DC2626
- Badge Success: bg #DCFCE7, text #166534, 13px, pill radius
- Table Header: #F1F5F9 bg, #334155 text 600, sticky, row 48dp

---

## Implementation Notes
- Prefer CSS variables for theming (light/dark);
- Use 4pt spacing scale; avoid one-off pixel values.
- Defer heavy content; show skeletons under 100ms; optimistic updates where safe with rollback.
- Icon set must remain consistent stroke and corner radius.
