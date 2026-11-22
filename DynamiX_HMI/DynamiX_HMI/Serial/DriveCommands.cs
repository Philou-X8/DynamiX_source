using System;

namespace DynamiX_HMI.Serial
{
    /// <summary>
    /// Builds Modbus RTU frames for the TD350 motor drive (see the drive manual, chapter 9).
    /// The ESP32 forwards these to the drive as-is.
    /// </summary>
    internal static class DriveCommands
    {
        public const byte DriveAddress = 0x03;      // Modbus slave address of the drive
        public const byte WriteSingleRegister = 0x06;

        // P00.10 "keypad set frequency", unit 0.01 Hz. 0x800A is the RAM-only alias of 0x000A
        // (manual 9.4.5.1: setting the top address bit writes to RAM and skips the EEPROM,
        // which wears out if written often).
        public const ushort FrequencyRegister = 0x800A;

        public const double MaxHz = 60.0;           // P00.03 max output frequency; the drive refuses more

        /// <summary>
        /// Frame that sets the drive's running frequency:
        /// [addr][0x06][reg hi][reg lo][value hi][value lo][crc lo][crc hi]
        /// </summary>
        public static byte[] BuildSetFrequency(double hz)
        {
            if (hz < 0 || hz > MaxHz)
            {
                throw new ArgumentOutOfRangeException(nameof(hz), "frequency must be 0 to " + MaxHz + " Hz");
            }

            int freq = (int)Math.Round(hz * 100.0); // register unit is 0.01 Hz
            byte[] msg = new byte[8];
            msg[0] = DriveAddress;
            msg[1] = WriteSingleRegister;
            msg[2] = (byte)(FrequencyRegister >> 8);
            msg[3] = (byte)(FrequencyRegister & 0xFF);
            msg[4] = (byte)(freq >> 8);
            msg[5] = (byte)(freq & 0xFF);

            ushort crc = Crc16(msg, 6);
            msg[6] = (byte)(crc & 0xFF); // low byte first
            msg[7] = (byte)(crc >> 8);
            return msg;
        }

        /// <summary> Modbus CRC-16 (poly 0xA001) of the first 'length' bytes. </summary>
        public static ushort Crc16(byte[] data, int length)
        {
            ushort crc = 0xFFFF;
            for (int i = 0; i < length; i++)
            {
                crc ^= data[i];
                for (int bit = 0; bit < 8; bit++)
                {
                    crc = (crc & 1) != 0 ? (ushort)((crc >> 1) ^ 0xA001) : (ushort)(crc >> 1);
                }
            }
            return crc;
        }
    }
}
