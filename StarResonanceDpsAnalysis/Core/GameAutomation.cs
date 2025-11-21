using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace StarResonanceDpsAnalysis.Core
{
    /// <summary>
    /// Automates interactions with the game window for bulk data collection
    /// </summary>
    public class GameAutomation
    {
        #region Windows API Imports

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern void mouse_event(uint dwFlags, int dx, int dy, uint dwData, UIntPtr dwExtraInfo);

        [DllImport("user32.dll")]
        private static extern bool SetCursorPos(int x, int y);

        private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        private const uint MOUSEEVENTF_LEFTUP = 0x0004;
        private const uint MOUSEEVENTF_MOVE = 0x0001;
        private const uint MOUSEEVENTF_ABSOLUTE = 0x8000;

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        #endregion

        /// <summary>
        /// Find the game window by process name or window title
        /// </summary>
        public static IntPtr FindGameWindow(string processName = "Blue Protocol")
        {
            // Common Blue Protocol window titles to try
            string[] possibleTitles = {
                "Blue Protocol: Star Resonance",  // Star Resonance version
                "BLUE PROTOCOL",
                "Blue Protocol",
                "ブループロトコル",
                "BlueProtocol"
            };

            // Try to find by window title first (most reliable)
            foreach (var title in possibleTitles)
            {
                IntPtr hWnd = FindWindow(null, title);
                if (hWnd != IntPtr.Zero)
                {
                    Console.WriteLine($"[AUTOMATION] Found game window: {title}");
                    return hWnd;
                }
            }

            // Fallback: Try to find by process name
            var processes = Process.GetProcessesByName(processName);
            if (processes.Length > 0 && processes[0].MainWindowHandle != IntPtr.Zero)
            {
                Console.WriteLine($"[AUTOMATION] Found game by process: {processName}");
                return processes[0].MainWindowHandle;
            }

            // Also try common process name variations
            string[] processNames = {
                "Blue Protocol",
                "BlueProtocol",
                "BLUE_PROTOCOL",
                "bp",
                "BP"
            };

            foreach (var name in processNames)
            {
                processes = Process.GetProcessesByName(name);
                if (processes.Length > 0 && processes[0].MainWindowHandle != IntPtr.Zero)
                {
                    Console.WriteLine($"[AUTOMATION] Found game by process: {name}");
                    return processes[0].MainWindowHandle;
                }
            }

            Console.WriteLine("[AUTOMATION] Game window not found!");
            return IntPtr.Zero;
        }

        /// <summary>
        /// Bring game window to foreground
        /// </summary>
        public static bool FocusGameWindow(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero)
                return false;

            return SetForegroundWindow(hWnd);
        }

        /// <summary>
        /// Click at specific coordinates within the game window
        /// </summary>
        /// <param name="hWnd">Game window handle</param>
        /// <param name="clientX">X coordinate relative to game window client area</param>
        /// <param name="clientY">Y coordinate relative to game window client area</param>
        public static void ClickAt(IntPtr hWnd, int clientX, int clientY)
        {
            if (hWnd == IntPtr.Zero)
                throw new InvalidOperationException("Invalid window handle");

            // Convert client coordinates to screen coordinates
            POINT pt = new POINT { X = clientX, Y = clientY };
            ClientToScreen(hWnd, ref pt);

            // Move cursor and click
            SetCursorPos(pt.X, pt.Y);
            Thread.Sleep(50); // Small delay for cursor to move

            mouse_event(MOUSEEVENTF_LEFTDOWN, pt.X, pt.Y, 0, UIntPtr.Zero);
            Thread.Sleep(50);
            mouse_event(MOUSEEVENTF_LEFTUP, pt.X, pt.Y, 0, UIntPtr.Zero);
        }

        /// <summary>
        /// Scroll within the game window by dragging from last item to first item position (mobile UI style)
        /// </summary>
        /// <param name="hWnd">Game window handle</param>
        /// <param name="dragStartX">X position of the list</param>
        /// <param name="dragStartY">Y position to start drag (last visible item)</param>
        /// <param name="dragEndY">Y position to end drag (first visible item)</param>
        public static void DragScrollList(IntPtr hWnd, int dragStartX, int dragStartY, int dragEndY)
        {
            if (hWnd == IntPtr.Zero)
                throw new InvalidOperationException("Invalid window handle");

            // Compensate for UI "dead zone" - game doesn't start scrolling until ~15-20px of movement
            const int deadZoneCompensation = 40;
            int compensatedEndY = dragEndY - deadZoneCompensation; // End higher (lower Y) to account for dead zone
            
            Console.WriteLine($"[AUTOMATION] Drag scroll from ({dragStartX}, {dragStartY}) to ({dragStartX}, {compensatedEndY}) (dead zone: {deadZoneCompensation}px)");

            // Convert to screen coordinates
            POINT startPt = new POINT { X = dragStartX, Y = dragStartY };
            ClientToScreen(hWnd, ref startPt);

            POINT endPt = new POINT { X = dragStartX, Y = compensatedEndY };
            ClientToScreen(hWnd, ref endPt);

            // Move to last item position
            SetCursorPos(startPt.X, startPt.Y);
            Thread.Sleep(50);

            // Click and hold on last item
            mouse_event(MOUSEEVENTF_LEFTDOWN, startPt.X, startPt.Y, 0, UIntPtr.Zero);
            Thread.Sleep(100);

            // Drag upward to first item position (scrolls list down)
            // Slower drag so UI can keep up - more steps with longer delays
            int steps = 20;
            for (int i = 1; i <= steps; i++)
            {
                int currentY = startPt.Y + (int)((endPt.Y - startPt.Y) * i / (float)steps);
                SetCursorPos(startPt.X, currentY);
                Thread.Sleep(20); // Longer delay so UI keeps up with cursor
            }

            // Hold at end position to stop momentum BEFORE releasing
            Thread.Sleep(800);
            
            // Release
            mouse_event(MOUSEEVENTF_LEFTUP, endPt.X, endPt.Y, 0, UIntPtr.Zero);
        }

        /// <summary>
        /// Get game window dimensions
        /// </summary>
        public static (int width, int height) GetWindowSize(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero)
                return (0, 0);

            GetClientRect(hWnd, out RECT rect);
            return (rect.Right - rect.Left, rect.Bottom - rect.Top);
        }

        /// <summary>
        /// Automated guild roster clicking
        /// </summary>
        /// <param name="memberCount">Number of guild members to click</param>
        /// <param name="startX">X position of first member</param>
        /// <param name="startY">Y position of first member</param>
        /// <param name="offsetY">Y offset between members</param>
        /// <param name="membersPerPage">How many members visible before scrolling</param>
        /// <param name="clickDelay">Delay between clicks (ms)</param>
        /// <param name="progress">Progress callback</param>
        /// <param name="cancellationToken">Cancellation token</param>
        public static async Task AutoClickGuildRoster(
            int memberCount,
            int startX,
            int startY,
            int offsetY,
            int membersPerPage,
            int clickDelay,
            Action<int, string>? progress = null,
            CancellationToken cancellationToken = default)
        {
            IntPtr gameWindow = FindGameWindow();
            if (gameWindow == IntPtr.Zero)
            {
                throw new InvalidOperationException("Game window not found! Please start the game first.");
            }

            progress?.Invoke(0, "Found game window, starting automation...");

            // Focus game window
            if (!FocusGameWindow(gameWindow))
            {
                throw new InvalidOperationException("Failed to focus game window!");
            }

            await Task.Delay(500, cancellationToken); // Wait for window to focus

            for (int i = 0; i < memberCount; i++)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    progress?.Invoke(i, "Cancelled by user");
                    break;
                }

                // Calculate position in list
                int positionInPage = i % membersPerPage;
                int currentY = startY + (positionInPage * offsetY);

                progress?.Invoke(i + 1, $"Clicking member {i + 1}/{memberCount} at ({startX}, {currentY})...");

                // Click on member
                ClickAt(gameWindow, startX, currentY);

                // Wait for response
                await Task.Delay(clickDelay, cancellationToken);

                // Always scroll after every membersPerPage clicks
                if (positionInPage == membersPerPage - 1)
                {
                    progress?.Invoke(i + 1, "Scrolling to next page...");
                    
                    // Drag from the 6th member's position (visible but not clicked) to the 1st member's position
                    // This scrolls the list down by exactly one page
                    int sixthMemberY = startY + (membersPerPage * offsetY);  // Position where 6th member is visible
                    int firstMemberY = startY;   // Original position of the first member
                    
                    DragScrollList(gameWindow, startX, sixthMemberY, firstMemberY);                    
                }
            }

            // After the last scroll, click all 6 visible positions on the final screen
            // (the screen shows ~5.5 items after scrolling, so click all positions to ensure we get everyone)
            progress?.Invoke(memberCount, "Final pass: clicking all 6 positions on last screen...");
            for (int pos = 0; pos <= membersPerPage; pos++)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;
                    
                int finalY = startY + (pos * offsetY);
                progress?.Invoke(memberCount, $"Final click at position {pos + 1}/6...");
                ClickAt(gameWindow, startX, finalY);
                await Task.Delay(clickDelay, cancellationToken);
            }

            // Report collection status
            var (isCollecting, count) = MasterScoreCollector.GetStatus();
            progress?.Invoke(memberCount, $"Automation complete! Master Score records collected: {count}");

        }
    }
}

