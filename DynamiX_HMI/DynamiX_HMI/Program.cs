using DynamiX_HMI.Serial;
using System.IO.Ports;

namespace DynamiX_HMI
{
    internal static class Program
    {
        //public static SerialPort serialPort;
        //private SerialController serialPort;

        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            // To customize application configuration such as set high DPI settings or default font,
            // see https://aka.ms/applicationconfiguration.
            ApplicationConfiguration.Initialize();

            //SerialForm serialForm = new SerialForm();
            MasterForm masterForm = new MasterForm();


            Application.Run(masterForm);

            //Form1 mainForm = new Form1();
            //SerialController serialPort = new SerialController();

            ////serialPort.AttachHandler(mainForm, mainForm.MessageReciever);
            ////serialPort.Reconnect();
            //serialPort.LinkHandler(mainForm.SerialDataHandle);
            //serialPort.LinkListHandler(mainForm.SerialListHandle);

            //mainForm.LinkHandler(serialPort.GuiDataHandle);

            //Application.Run(serialForm);
            
        }
    }
}