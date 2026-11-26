using DynamiX_HMI.Serial;
using System.Drawing;
using System.Windows.Forms;

namespace DynamiX_HMI.Menus
{
    public enum LogLevel { Normal, Error, Debug }

    /// <summary>
    /// The status line and the message feed. Each message goes on a new line at the top of the feed (black for a
    /// normal message, red for an error, gray for debug text); the feed keeps the last MaxMessages.
    /// Everything runs on the UI thread.
    /// </summary>
    internal class Logger
    {
        public Dictionary<LogLevel, bool> visibilityMap;
        private const int MaxDisplay = 10;
        private const int MaxMessages = 50;

        private readonly Button connectionButton;
        private readonly RichTextBox feed;
        private readonly Label stateLabel;
        private readonly LinkedList<(string text, LogLevel level)> messages = new LinkedList<(string, LogLevel)>(); // newest first

        public Logger(Button btn, RichTextBox messageFeed, Label sta)
        {
            connectionButton = btn;
            feed = messageFeed;
            stateLabel = sta;

            visibilityMap = new Dictionary<LogLevel, bool>();
            visibilityMap[LogLevel.Normal] = true;
            visibilityMap[LogLevel.Error] = true;
            visibilityMap[LogLevel.Debug] = false;
        }

        public void SetButton(string txt)
        {
            connectionButton.Text = txt;
        }

        /// <summary> A message; the level comes from its start (ERROR, DENIED, PANIC, Warning, Could not: an error). </summary>
        public void SetMessage(string txt)
        {
            SetMessage(txt, LooksLikeError(txt) ? LogLevel.Error : LogLevel.Normal);
        }

        public void SetMessage(string txt, LogLevel level)
        {
            messages.AddFirst((DateTime.Now.ToString("HH:mm:ss") + "  " + txt.TrimEnd('\r', '\n'), level));
            while (messages.Count > MaxMessages)
            {
                messages.RemoveLast();
            }
            Redraw();
        }

        /// <summary> A text packet from the ESP32: ESP_error in red, ESP_debug in gray, anything else normal. </summary>
        public void SetMessage(CommandPacket cmd)
        {
            LogLevel level = cmd.flag == CommandFlags.ESP_error ? LogLevel.Error
                           : cmd.flag == CommandFlags.ESP_debug ? LogLevel.Debug
                           : LogLevel.Normal;
            SetMessage(cmd.payload, level);
        }

        public void SetState(string txt)
        {
            stateLabel.Text = txt;
        }

        public void ToggleVilisbity(LogLevel level, bool visibility)
        {
            visibilityMap[level] = visibility;
            Redraw();
        }

        public void ToggleVilisbity(LogLevel level)
        {
            visibilityMap[level] = !visibilityMap[level];
            Redraw();
        }

        private static bool LooksLikeError(string txt)
        {
            return txt.StartsWith("ERROR", StringComparison.OrdinalIgnoreCase)
                || txt.StartsWith("DENIED", StringComparison.OrdinalIgnoreCase)
                || txt.StartsWith("PANIC", StringComparison.OrdinalIgnoreCase)
                || txt.StartsWith("Warning", StringComparison.OrdinalIgnoreCase)
                || txt.StartsWith("Could not", StringComparison.OrdinalIgnoreCase);
        }

        private void Redraw()
        {
            feed.SuspendLayout();
            feed.Clear();
            int i = 0;
            foreach ((string text, LogLevel level) in messages)
            {
                if (visibilityMap[level] == false)
                {
                    continue; // dont show this message
                }
                feed.SelectionStart = feed.TextLength;
                feed.SelectionLength = 0;
                feed.SelectionColor = level == LogLevel.Error ? Color.Firebrick : level == LogLevel.Debug ? Color.Gray : Color.Black;
                feed.AppendText(text + "\n");
                i++;
                if (i >= MaxDisplay) break; // showing enough messages
            }
            feed.SelectionStart = 0; // the newest message, at the top
            feed.ScrollToCaret();
            feed.ResumeLayout();
        }
    }
}
