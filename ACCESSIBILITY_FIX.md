# PerfView Accessibility Improvements

## Issue: NVDA Screen Reader Incorrect List Count

### Problem
Screen readers like NVDA were announcing incorrect item counts for the File Explorer tree view, saying "1 of 9" instead of "1 of 6" when there were only 6 visible files.

### Root Cause
The TreeView accessibility system was counting:
1. All files in the directory (6 visible files)
2. The ".." parent directory entry (always added for navigation)
3. Hidden system files that weren't filtered out
4. UI elements like context menus and decorative images

### Solution
Implemented targeted accessibility improvements:

1. **System File Filtering**: Enhanced file filtering in `PerfViewDirectory.Children` to exclude:
   - Hidden files starting with "." or "~"
   - Windows system files like "desktop.ini", "thumbs.db"
   - System directories like "$RECYCLE.BIN", "System Volume Information"

2. **Accessibility Exclusions**: Used WPF `AutomationProperties.AccessibilityView="Raw"` to exclude from screen reader counts:
   - The ".." parent directory (while keeping it functional for navigation)
   - Context menus and decorative images
   - Other UI elements that aren't actual file items

3. **Improved Screen Reader Experience**: Added helpful text and proper naming for accessibility tools.

### Files Modified
- `src/PerfView/MainWindow.xaml`: TreeView accessibility improvements
- `src/PerfView/PerfViewData.cs`: Enhanced system file filtering
- `src/PerfView.Tests/AccessibilityTests.cs`: Test coverage for the fix

### Testing
Created unit tests to verify that system files are properly filtered and the correct item counts are maintained for accessibility.