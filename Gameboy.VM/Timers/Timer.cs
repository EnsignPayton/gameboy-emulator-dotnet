﻿using System;

namespace Gameboy.VM.Timers
{
    internal class Timer
    {
        internal long TotalTCycles;
        internal ushort SystemCounter { get; private set; }

        private readonly Device _device;
        private int _internalCount;

        internal Timer(Device device)
        {
            _device = device;
        }

        internal void Reset(bool skipBootrom)
        {
            if (!skipBootrom)
            {
                SystemCounter = 0x0;
                return;
            }

            SystemCounter = (_device.Type, _device.Mode) switch
            {
                (DeviceType.DMG, DeviceType.DMG) => 0xABCC,
                (DeviceType.CGB, DeviceType.DMG) => 0x267C,
                (DeviceType.CGB, DeviceType.CGB) => 0x1EA0,
                _ => throw new ArgumentOutOfRangeException(nameof(_device.Type), $"Invalid device type & mode combination ({_device.Type}, {_device.Mode})")
            };
        }

        internal void Step(int tCycles)
        {
            // Handle system counter - note that it happens regardless of whether timer is turned on
            SystemCounter = (ushort)(SystemCounter + tCycles);
            TotalTCycles += tCycles;

            // Handle standard timer
            if (!_isTimerEnabled) return;

            _internalCount += tCycles;

            while (_internalCount >= _timerClockSelect.Step())
            {
                if (TimerCounter == 0xFF)
                {
                    TimerCounter = TimerModulo;
                    _device.InterruptRegisters.RequestInterrupt(Interrupts.Interrupt.Timer);
                }
                else
                {
                    TimerCounter = (byte)(TimerCounter + 1);
                }

                _internalCount -= _timerClockSelect.Step();
            }
        }

        #region Timer Registers
        private bool _isTimerEnabled;
        private TimerClockSelect _timerClockSelect;

        private byte _timerController = 0b11111000;
        internal byte TimerController
        {
            get => _timerController;
            set
            {
                _timerController = (byte)((value & 0x7) | 0b11111000); // Unused bits always return 1
                _isTimerEnabled = (value & 0x4) == 0x4;
                _timerClockSelect = (TimerClockSelect)(value & 0x3);
            }
        }

        internal byte Divider
        { 
            get => (byte)(SystemCounter >> 8); // DIV is just 8MSB of system counter
            set
            {
                SystemCounter = 0;
                _internalCount = 0x0;
            }
        }
        internal byte TimerCounter { get; set; }
        internal byte TimerModulo { get; set; }
        #endregion

        public override string ToString()
        {
            return $"DIV:{Divider:X2}, TAC:{TimerCounter:X2}, TIM:{TimerModulo:X2}, TC:{TimerController:X2}, Internal:{_internalCount:X2}";
        }
    }
}
