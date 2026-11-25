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
        private const int MaxMessages = 10;

        private readonly Button connectionButton;
        private readonly RichTextBox feed;
        private readonly Label stateLabel;
        private readonly LinkedList<(string text, LogLevel level)> messages = new LinkedList<(string, LogLevel)>(); // newest first

        public Logger(Button btn, RichTextBox messageFeed, Label sta)
        {
            connectionButton = btn;
            feed = messageFeed;
            stateLabel = sta;
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
            foreach ((string text, LogLevel level) in messages)
            {
                feed.SelectionStart = feed.TextLength;
                feed.SelectionLength = 0;
                feed.SelectionColor = level == LogLevel.Error ? Color.Firebrick : level == LogLevel.Debug ? Color.Gray : Color.Black;
                feed.AppendText(text + "\n");
            }
            feed.SelectionStart = 0; // the newest message, at the top
            feed.ScrollToCaret();
            feed.ResumeLayout();
        }
    }
}
