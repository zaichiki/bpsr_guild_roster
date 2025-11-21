# Guild Roster Automation Guide

## Overview

This system automates clicking through your guild roster to streamline data collection. It simulates mouse clicks on each guild member, allowing you to view their profiles automatically.

## How It Works

1. **Find Game Window** - Locates the Blue Protocol window
2. **Simulate Clicks** - Uses Windows API to click on each guild member
3. **Auto-Scroll** - Scrolls down as needed to reach all members
4. **View Profiles** - Each click opens the player's profile for viewing

## Setup Steps

### 1. Open Guild Roster
- In the game, open your Guild menu
- Navigate to the Member List
- Make sure the list is fully visible
- Position the window how you want it

### 2. Configure Coordinates (Important!)

Open `Forms/DebugWindowForm.cs` and find the `button_Test2_Click` method. Adjust these values:

```csharp
int memberCount = 80;           // Total guild members
int startX = 200;               // X position of member name (from left edge of window)
int startY = 150;               // Y position of first member (from top of window)
int offsetY = 50;               // Vertical spacing between members
int membersPerPage = 8;         // How many members visible at once
int clickDelay = 1500;          // Wait time between clicks (ms)
```

**To find the correct coordinates:**
1. Take a screenshot of your guild roster
2. Use Paint or an image editor to measure pixel positions
3. Count the pixel distance from the LEFT edge of the window to where the member names are
4. Count the pixel distance from the TOP of the window to the first member name
5. Measure the vertical spacing between member names
6. **Test with a small `memberCount` (like 5) first!**

### 3. Test First!

Before running on all 80 members:
1. Set `memberCount = 5` to test with just 5 members
2. Run the automation
3. Watch carefully to see if clicks are accurate
4. Adjust coordinates if needed
5. Once working, increase to full member count

### 4. Run Automation

1. Open the **Debug Window** from the main menu
2. Make sure the guild roster is open and visible in the game
3. Click **"Auto-Click Guild Roster"** button
4. **Don't touch your mouse/keyboard during automation!**
5. Watch the progress in the debug output window
6. To stop early, click the button again (it will say "STOP Automation")

### 5. Monitor Progress

The debug window will show:
- `[0/80] Found game window, starting automation...`
- `[1/80] Clicking member 1/80...`
- `[8/80] Scrolling to next page...`
- `[80/80] Automation complete!`

## Troubleshooting

### "Game window not found!"
- Make sure Blue Protocol is running
- The window must be the **main game window** (not launcher)
- Window title should be **"Blue Protocol: Star Resonance"** (already configured)
- Check Task Manager for the exact process name if it still fails
- Try restarting the game if window detection fails

### Clicks are off-target
- **This is the most common issue!**
- Adjust `startX`, `startY`, and `offsetY` values
- Different screen resolutions need different coordinates
- UI scaling in game/Windows affects pixel positions
- Take a screenshot and measure carefully

### Not scrolling correctly
- **This game uses drag-to-scroll** (mobile-style UI), not mouse wheel
- Adjust `membersPerPage` to match your actual visible members (typically 6)
- Change the drag distance: Find `DragScroll(gameWindow, -200)` in `GameAutomation.cs`
- Try `-150`, `-200`, or `-250` depending on how far you need to scroll
- Increase the scroll delay (currently 500ms) if animation is slow

### Game loses focus during automation
- Don't click outside the game window
- Disable notifications/popups that might steal focus
- Close other applications that might interrupt
- Use a second monitor to watch progress without clicking

### Automation too fast or too slow
- Increase `clickDelay` if profiles aren't loading in time
- Decrease `clickDelay` if you want it to go faster
- Typical range: 1000-2000ms per click

## Important Coordinate Tips

### Finding startX and startY
1. Open guild roster in game
2. Take a screenshot (Win+Print Screen)
3. Open in Paint or similar
4. Move mouse over the first member's name
5. Note the X,Y coordinates in the bottom-left of Paint
6. These are your `startX` and `startY` values!

### Finding offsetY
1. Measure Y coordinate of first member
2. Measure Y coordinate of second member
3. Subtract: second Y - first Y = offsetY
4. Typical value: 85-90 pixels for the Blue Protocol UI

### Default Coordinate Set (Measured from Screenshot)

**1920x1080 Guild Roster UI:**
```csharp
startX = 300;        // Player name column
startY = 245;        // First member position
offsetY = 88;        // Spacing between members
membersPerPage = 6;  // Visible members before scroll
```

**If your UI looks different:**
- Check your Windows display scaling (100%, 125%, 150%)
- Measure from your own screenshot
- Test with small batch first (memberCount = 5)

*Your mileage may vary - always test with a small batch first!*

## Safety Notes

- ✅ Uses **Windows mouse simulation** - completely client-side
- ✅ Only **simulates clicking** - no game modification
- ✅ Can be stopped at any time with one click
- ✅ No network interaction - just automates UI navigation
- ⚠️ **Test thoroughly** with small batches first
- ⚠️ **Don't use** if game explicitly prohibits automation tools

## Performance Tips

- **Always test with 5-10 members first** before full run
- **Increase clickDelay** if profiles load slowly
- **Run when you have time** - 80 members × 1.5 seconds = ~2 minutes
- **Watch the first few clicks** to ensure accuracy
- **Use windowed mode** if you need to monitor other things

## What to Do After

After automation completes, you'll have opened all guild member profiles. You can:
- Manually review the information
- Take notes on active/inactive members
- Screenshot profiles for records
- Use this to audit your guild roster

## Future Improvements

Potential enhancements for this tool:
- [ ] GUI for coordinate configuration (no code editing needed)
- [ ] Click position calibration wizard
- [ ] Visual overlay showing where it will click
- [ ] Automatic coordinate detection via screen capture
- [ ] Progress bar in the UI
- [ ] Pause/resume functionality
- [ ] Random click delays to appear more natural

## Example Output

```
========================================
[00:05:30.123] Starting Guild Roster Automation
========================================

Configuration:
  Total Members: 80
  Click Position: (200, 150)
  Members per Page: 8
  Click Delay: 1500ms

NOTE: Adjust these values in code if needed!
      Window must stay focused on the guild roster.

[0/80] Found game window, starting automation...
[1/80] Clicking member 1/80...
[2/80] Clicking member 2/80...
[3/80] Clicking member 3/80...
[4/80] Clicking member 4/80...
[5/80] Clicking member 5/80...
[6/80] Clicking member 6/80...
[7/80] Clicking member 7/80...
[8/80] Clicking member 8/80...
[8/80] Scrolling to next page...
[9/80] Clicking member 9/80...
...
[80/80] Automation complete!

========================================
Automation completed successfully!
========================================
```

## Disclaimer

This tool automates mouse clicking to navigate the game's UI. It does not:
- Modify game files
- Inject code into the game process
- Intercept or modify network traffic
- Provide any gameplay advantage

Use responsibly and in accordance with the game's Terms of Service. If unsure about automation tools, check with the game's support team or community guidelines first.

---

**Good luck with your guild management!** 🎮

