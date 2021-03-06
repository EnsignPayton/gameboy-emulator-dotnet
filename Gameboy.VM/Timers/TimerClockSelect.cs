﻿using System;
using System.Diagnostics.CodeAnalysis;

namespace Gameboy.VM.Timers
{
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    internal enum TimerClockSelect
    {
        f_2_4 = 0x00, // 4.096 KHz
        f_2_10 = 0x01, // 262.144 KHz
        f_2_6 = 0x02, // 65.536 KHz
        f_2_8 = 0x03, // 16.384 KHz
    }

    internal static class TimerClockSelectExtensions
    {
        internal static int Step(this TimerClockSelect timerClockSelect) => timerClockSelect switch
        {
            TimerClockSelect.f_2_4 => 1024,
            TimerClockSelect.f_2_10 => 16,
            TimerClockSelect.f_2_6 => 64,
            TimerClockSelect.f_2_8 => 256,
            _ => throw new ArgumentOutOfRangeException(nameof(timerClockSelect), timerClockSelect, "Unhandled TimerClockSelect value")
        };
    }
}
