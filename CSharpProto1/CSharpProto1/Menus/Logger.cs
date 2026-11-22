using CSharpProto1.Serial;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CSharpProto1.Menus
{
    internal class Logger
    {
        private string buttonText;
        private string messageText;
        private string stateText;

        private Button connectionButton;
        private Label messageLabel;
        private Label stateLabel;

        public Logger(Button btn, Label msg, Label sta)
        {
            connectionButton = btn;
            messageLabel = msg;
            stateLabel = sta;

            buttonText = "";
            messageText = "";
            stateText = "";
        }

        public void SetButton(string txt)
        {
            if(connectionButton != null)
            {
                connectionButton.Text = txt;
            }
        }

        public void SetMessage(string txt)
        {
            if(messageLabel != null)
            {
                messageLabel.Text = txt;
            }
        }

        public void SetMessage(CommandPacket cmd)
        {
            if(messageLabel != null)
            {
                messageLabel.Text = cmd.payload;
            }
        }

        public void SetState(string txt)
        {
            if(stateLabel != null)
            {
                stateLabel.Text = txt;
            }
        }
    }
}
