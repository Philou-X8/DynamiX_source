using DynamiX_HMI.Serial;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DynamiX_HMI.Sequencer
{
    public enum StateTransition
    {
        None,
        Disconnected,
        Connecting,
        Connected, // idle
        Tar,
        Requesting,
        Calibrating,
        Starting,
        Running,
        Finished,
        Panic,
    }


    internal class MachineController
    {
        
        private Func<CommandPacket, StateTransition> StateDelegate;
        //private delegate StateTransition StateCommandDelegate(CommandPacket command);

        public MachineController(SerialController controller)
        {

            StateDelegate = DoStateDisconnected;

        }

        /// <summary> True in Idle: nothing running, a run or a TAR can be started. </summary>
        public bool IsIdle => StateDelegate == DoStateIdle;

        public StateTransition CommandTransition(CommandPacket command)
        {
            //StateDelegate = IdleState;
            
            if (command.flag == CommandFlags.GUI_disconnected)
            {
                StateDelegate = DoStateDisconnected;
                return StateTransition.Disconnected;
            }
            else if (command.flag == CommandFlags.GUI_check_connection)
            {
                StateDelegate = DoStateConnecting;
                return StateTransition.Connecting;
            }
            // The ESP answered a heartbeat with "not connected": it rebooted, or it dropped us after
            // a silence and stopped the motor. Whatever we thought we were doing is over: shake hands again.
            else if (command.flag == CommandFlags.ESP_not_connected
                     && StateDelegate != DoStateDisconnected
                     && StateDelegate != DoStateConnecting)
            {
                StateDelegate = DoStateConnecting;
                return StateTransition.Connecting;
            }
            // The ESP stopped the motor (e-stop, door, drive fault, or a fault it found itself):
            // Panic from any connected state. The ESP sends it again on each reconnection.
            else if (command.flag == CommandFlags.ESP_emergency
                     && StateDelegate != DoStateDisconnected
                     && StateDelegate != DoStateConnecting)
            {
                if (StateDelegate == DoStatePanic)
                {
                    return StateTransition.None; // already there
                }
                StateDelegate = DoStatePanic;
                return StateTransition.Panic;
            }

            // Change the state handler
            StateTransition transitionType = StateDelegate(command);

            return transitionType;
        }

        private StateTransition DoStateDisconnected(CommandPacket command)
        {
            if(command.flag == CommandFlags.GUI_check_connection)
            {
                StateDelegate = DoStateConnecting;
                return StateTransition.Connecting;
            }
            return StateTransition.None;
        }

        private StateTransition DoStateConnecting(CommandPacket command)
        {
            if (command.flag == CommandFlags.GUI_check_connection) 
            {
                StateDelegate = DoStateConnecting;
                return StateTransition.Connecting;
            }
            if (command.flag == CommandFlags.ESP_good_connection)
            {
                StateDelegate = DoStateIdle;
                return StateTransition.Connected;
            }
            return StateTransition.None;
        }

        private StateTransition DoStateIdle(CommandPacket command)
        {
            // BASICALLY, these state functions needs to check the conditions 
            // required to change state. They are triggered when a command is recieved.

            // State could also run 

            switch (command.flag)
            {
                case CommandFlags.GUI_disconnected:
                    StateDelegate = DoStateDisconnected;
                    return StateTransition.Disconnected;
                case CommandFlags.GUI_click_start:
                    StateDelegate = DoStateRequesting;
                    return StateTransition.Requesting;
                case CommandFlags.GUI_TAR_request:
                    StateDelegate = DoStateTar;
                    return StateTransition.Tar;
                default:
                    return StateTransition.None;
            }

        }

        // TAR running on the ESP: no run can be started until it ends.
        private StateTransition DoStateTar(CommandPacket command)
        {
            switch (command.flag)
            {
                case CommandFlags.ESP_tar_data:         // TAR done (the new stroke and empty load)
                    StateDelegate = DoStateIdle;
                    return StateTransition.Connected;
                case CommandFlags.ESP_run_denied:       // TAR refused
                    StateDelegate = DoStateIdle;
                    return StateTransition.Connected;
                case CommandFlags.GUI_abort:
                    StateDelegate = DoStateIdle;
                    return StateTransition.Connected;
                default:
                    return StateTransition.None;
            }
        }
        private StateTransition DoStateRequesting(CommandPacket command)
        {
            //if (command.flag == CommandFlags.ESP_run_accepted)
            //{
            //    StateDelegate = DoStateStarting;
            //    return StateTransition.Starting;
            //}
            //return StateTransition.None;

            // Need to check for ESP refusal message
            // those can be caused by the drive not being able to recieve commands

            switch (command.flag)
            {
                case CommandFlags.ESP_run_accepted:
                    StateDelegate = DoStateCalibrating;
                    return StateTransition.Calibrating;
                case CommandFlags.ESP_run_denied:
                    StateDelegate = DoStateIdle;
                    return StateTransition.Connected;
                case CommandFlags.GUI_abort:
                    StateDelegate = DoStateIdle;
                    return StateTransition.Connected;
                default:
                    return StateTransition.None;
            }
        }
        private StateTransition DoStateCalibrating(CommandPacket command)
        {
            switch (command.flag)
            {
                case CommandFlags.ESP_calibration_data:
                    StateDelegate = DoStateStarting;
                    return StateTransition.Starting;
                case CommandFlags.ESP_run_denied:
                    StateDelegate = DoStateIdle;
                    return StateTransition.Connected;
                case CommandFlags.GUI_abort:
                    StateDelegate = DoStateIdle;
                    return StateTransition.Connected;
                default:
                    return StateTransition.None;
            }
        }

        private StateTransition DoStateStarting(CommandPacket command)
        {
            switch (command.flag)
            {
                case CommandFlags.ESP_run_starting:
                    StateDelegate = DoStateRunning;
                    return StateTransition.Running;
                case CommandFlags.ESP_run_denied:
                    StateDelegate = DoStateIdle;
                    return StateTransition.Connected;
                case CommandFlags.GUI_abort:
                    StateDelegate = DoStateIdle;
                    return StateTransition.Connected;
                default:
                    return StateTransition.None;
            }
        }
        private StateTransition DoStateRunning(CommandPacket command)
        {
            switch (command.flag)
            {
                case CommandFlags.ESP_run_finished:
                    StateDelegate = DoStateFinished;
                    return StateTransition.Finished;
                case CommandFlags.GUI_abort:
                    StateDelegate = DoStateIdle;
                    return StateTransition.Connected;
                default:
                    return StateTransition.None;
            }
        }


        private StateTransition DoStateFinished(CommandPacket command)
        {
            switch (command.flag)
            {
                case CommandFlags.GUI_run_completed:
                    StateDelegate = DoStateIdle;
                    return StateTransition.Connected;
                case CommandFlags.GUI_abort:
                    StateDelegate = DoStateIdle;
                    return StateTransition.Connected;
                default:
                    return StateTransition.None;
            }
        }

        // Motor stopped by the ESP. Only the ESP leaves Panic: once the safety circuit is
        // closed again (yellow reset button), plus a GUI_abort (Stop) if the ESP itself
        // found the fault. Stop is sent to the ESP but doesn't change the state here.
        private StateTransition DoStatePanic(CommandPacket command)
        {
            switch (command.flag)
            {
                case CommandFlags.ESP_emergency_clear:
                    StateDelegate = DoStateIdle;
                    return StateTransition.Connected;

                default:
                    return StateTransition.None;
            }
        }


    }
}
