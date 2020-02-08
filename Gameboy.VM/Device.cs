﻿using System;
using Gameboy.VM.LCD;
using Gameboy.VM.Interrupts;
using Gameboy.VM.Sound;
using System.Runtime.CompilerServices;
using Gameboy.VM.Cartridge;
using Gameboy.VM.Timers;
using Serilog;
using Serilog.Core;
using Gameboy.VM.Joypad;

[assembly: InternalsVisibleTo("Gameboy.VM.Tests")]
namespace Gameboy.VM
{
    /// <summary>
    /// A device here refers to the entire of an emulated gameboy and provides
    /// the public interface into this library.
    /// </summary>
    public class Device
    {
        public const int ScreenWidth = 160;
        public const int ScreenHeight = 144;
        public const int CyclesPerSecondHz = 4_194_304; // 4.194304 MHz

        public long TCycles = 0;

        /// <summary>
        /// Original ROM from a DMG, used to set initial values of registers
        /// </summary>
        internal static readonly byte[] DmgRomContents =
        {
            0x31, 0xFE, 0xFF, 0xAF, 0x21, 0xFF, 0x9F, 0x32, 0xCB, 0x7C, 0x20, 0xFB, 0x21, 0x26, 0xFF, 0x0E,
            0x11, 0x3E, 0x80, 0x32, 0xE2, 0x0C, 0x3E, 0xF3, 0xE2, 0x32, 0x3E, 0x77, 0x77, 0x3E, 0xFC, 0xE0,
            0x47, 0x11, 0x04, 0x01, 0x21, 0x10, 0x80, 0x1A, 0xCD, 0x95, 0x00, 0xCD, 0x96, 0x00, 0x13, 0x7B,
            0xFE, 0x34, 0x20, 0xF3, 0x11, 0xD8, 0x00, 0x06, 0x08, 0x1A, 0x13, 0x22, 0x23, 0x05, 0x20, 0xF9,
            0x3E, 0x19, 0xEA, 0x10, 0x99, 0x21, 0x2F, 0x99, 0x0E, 0x0C, 0x3D, 0x28, 0x08, 0x32, 0x0D, 0x20,
            0xF9, 0x2E, 0x0F, 0x18, 0xF3, 0x67, 0x3E, 0x64, 0x57, 0xE0, 0x42, 0x3E, 0x91, 0xE0, 0x40, 0x04,
            0x1E, 0x02, 0x0E, 0x0C, 0xF0, 0x44, 0xFE, 0x90, 0x20, 0xFA, 0x0D, 0x20, 0xF7, 0x1D, 0x20, 0xF2,
            0x0E, 0x13, 0x24, 0x7C, 0x1E, 0x83, 0xFE, 0x62, 0x28, 0x06, 0x1E, 0xC1, 0xFE, 0x64, 0x20, 0x06,
            0x7B, 0xE2, 0x0C, 0x3E, 0x87, 0xE2, 0xF0, 0x42, 0x90, 0xE0, 0x42, 0x15, 0x20, 0xD2, 0x05, 0x20,
            0x4F, 0x16, 0x20, 0x18, 0xCB, 0x4F, 0x06, 0x04, 0xC5, 0xCB, 0x11, 0x17, 0xC1, 0xCB, 0x11, 0x17,
            0x05, 0x20, 0xF5, 0x22, 0x23, 0x22, 0x23, 0xC9, 0xCE, 0xED, 0x66, 0x66, 0xCC, 0x0D, 0x00, 0x0B,
            0x03, 0x73, 0x00, 0x83, 0x00, 0x0C, 0x00, 0x0D, 0x00, 0x08, 0x11, 0x1F, 0x88, 0x89, 0x00, 0x0E,
            0xDC, 0xCC, 0x6E, 0xE6, 0xDD, 0xDD, 0xD9, 0x99, 0xBB, 0xBB, 0x67, 0x63, 0x6E, 0x0E, 0xEC, 0xCC,
            0xDD, 0xDC, 0x99, 0x9F, 0xBB, 0xB9, 0x33, 0x3E, 0x3C, 0x42, 0xB9, 0xA5, 0xB9, 0xA5, 0x42, 0x3C,
            0x21, 0x04, 0x01, 0x11, 0xA8, 0x00, 0x1A, 0x13, 0xBE, 0x20, 0xFE, 0x23, 0x7D, 0xFE, 0x34, 0x20,
            0xF5, 0x06, 0x19, 0x78, 0x86, 0x23, 0x05, 0x20, 0xFB, 0x86, 0x20, 0xFE, 0x3E, 0x01, 0xE0, 0x50
        };

        public readonly DeviceType Mode;
        public readonly DeviceType Type;
        internal readonly IRenderer Renderer;
        internal readonly ISoundOutput SoundOutput;
        internal readonly MMU MMU;
        internal readonly CPU.CPU CPU;
        internal readonly ControlRegisters ControlRegisters;
        internal readonly APU APU;
        internal readonly LCDRegisters LCDRegisters;
        internal readonly InterruptRegisters InterruptRegisters;
        internal readonly Cartridge.Cartridge Cartridge;
        internal readonly LCDDriver LCDDriver;
        internal readonly Timer Timer;
        internal readonly DMAController DMAController;
        internal readonly JoypadHandler JoypadHandler;

        // TODO - This isn't actually used to imply double speed anywhere yet
        internal bool DoubleSpeed = false;

        internal Logger Log;

        public void SetDebugMode()
        {
            Console.WriteLine("Setting debug mode");
            Log = new LoggerConfiguration()
                .MinimumLevel.Information()
                .WriteTo.File("log-debug.txt", outputTemplate: "{Message}{NewLine}", rollOnFileSizeLimit: true, buffered: true)
                .CreateLogger();
        }

        public Device(Cartridge.Cartridge cartridge, DeviceType type, IRenderer renderer, ISoundOutput soundOutput)
        {
            Log = new LoggerConfiguration()
                .MinimumLevel.Error()
                .CreateLogger();

            // A bit of double checking that we're loading a valid cartridge for the device type
            if (cartridge.CGBSupportCode == CGBSupportCode.CGBExclusive && type == DeviceType.DMG)
            {
                Log.Error("Cartridge can't be loaded because it's CGB only and this device was created as DMG");
                throw new ApplicationException("Cartridge can't be loaded because it's CGB only and this device was created as DMG");
            }

            Type = type;
            Mode = cartridge.CGBSupportCode switch
            {
                CGBSupportCode.CGBExclusive => DeviceType.CGB,
                CGBSupportCode.CGBCompatible => type,
                CGBSupportCode.CGBIncompatible => DeviceType.DMG,
                _ => throw new ArgumentOutOfRangeException()
            };

            Renderer = renderer;
            SoundOutput = soundOutput;

            InterruptRegisters = new InterruptRegisters();
            ControlRegisters = new ControlRegisters();
            APU = new APU(this);
            LCDRegisters = new LCDRegisters(this);
            Cartridge = cartridge;
            MMU = new MMU(DmgRomContents, this);
            CPU = new CPU.CPU(this);
            LCDDriver = new LCDDriver(this);
            Timer = new Timer(this);
            DMAController = new DMAController(this);
            JoypadHandler = new JoypadHandler(this);
        }

        /// <summary>
        /// Debug information about the LCD driver & registers
        /// </summary>
        /// <returns>
        /// 1. The contents of VRAM bank 0
        /// 2. The contents of VRAM bank 1
        /// 3. The contents of OAM RAM
        /// 3. The contents of the CGB BG Palette
        /// 4. The contents of the CGB Sprite Palette
        /// 5. The contents of the framebuffer
        /// </returns>
        public (byte[], byte[], byte[], (byte, byte, byte)[], (byte, byte, byte)[], byte[]) DumpLcdDebugInformation()
        {
            var (bank0, bank1, oam) = LCDDriver.DumpVRAM();
            return (
                bank0,
                bank1,
                oam,
                LCDRegisters.CGBBackgroundPalette.Palette,
                LCDRegisters.CGBSpritePalette.Palette,
                LCDDriver.GetCurrentFrame()
            );
        }

        /// <summary>
        /// Sets registers/memory to initial values as if we've run the boot 
        /// rom successfully.
        /// </summary>
        public void SkipBootRom()
        {
            // Set up registers
            switch (Type, Mode)
            {
                case (DeviceType.DMG, DeviceType.DMG): // DMG device running a DMG cartridge
                    CPU.Registers.AF = 0x01B0;
                    CPU.Registers.BC = 0x0013;
                    CPU.Registers.DE = 0x00D8;
                    CPU.Registers.HL = 0x014D;
                    break;
                case (DeviceType.CGB, DeviceType.DMG): // CGB device running a DMG cartridge
                    CPU.Registers.AF = 0x1180;
                    CPU.Registers.BC = 0x0000;
                    CPU.Registers.DE = 0x0008;
                    CPU.Registers.HL = 0x007C;
                    break;
                case (DeviceType.CGB, DeviceType.CGB): // CGB device running a CGB cartridge
                    CPU.Registers.AF = 0x1180;
                    CPU.Registers.BC = 0x0000;
                    CPU.Registers.DE = 0xFF56;
                    CPU.Registers.HL = 0x000D;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            // Same across all device types to the best available knowledge
            CPU.Registers.ProgramCounter = 0x0100;
            CPU.Registers.StackPointer = 0xFFFE;

            // Set up memory
            MMU.WriteByte(0xFF05, 0);
            MMU.WriteByte(0xFF06, 0);
            MMU.WriteByte(0xFF07, 0);
            MMU.WriteByte(0xFF10, 0x80);
            MMU.WriteByte(0xFF11, 0xBF);
            MMU.WriteByte(0xFF12, 0xF3);
            MMU.WriteByte(0xFF14, 0xBF);
            MMU.WriteByte(0xFF16, 0x3F);
            MMU.WriteByte(0xFF16, 0x3F);
            MMU.WriteByte(0xFF17, 0);
            MMU.WriteByte(0xFF19, 0xBF);
            MMU.WriteByte(0xFF1A, 0x7F);
            MMU.WriteByte(0xFF1B, 0xFF);
            MMU.WriteByte(0xFF1C, 0x9F);
            MMU.WriteByte(0xFF1E, 0xFF);
            MMU.WriteByte(0xFF20, 0xFF);
            MMU.WriteByte(0xFF21, 0);
            MMU.WriteByte(0xFF22, 0);
            MMU.WriteByte(0xFF23, 0xBF);
            MMU.WriteByte(0xFF24, 0x77);
            MMU.WriteByte(0xFF25, 0xF3);
            MMU.WriteByte(0xFF26, 0xF1);
            MMU.WriteByte(0xFF40, 0x91);
            MMU.WriteByte(0xFF42, 0);
            MMU.WriteByte(0xFF43, 0);
            MMU.WriteByte(0xFF45, 0);
            MMU.WriteByte(0xFF47, 0xFC);
            MMU.WriteByte(0xFF48, 0xFF);
            MMU.WriteByte(0xFF49, 0xFF);
            MMU.WriteByte(0xFF4A, 0);
            MMU.WriteByte(0xFF4B, 0);
            MMU.WriteByte(0xFF50, 0x1); // Turn off boot ROM

            Timer.Reset(true); // Set up system counter (aka DIV register)
        }

        /// <summary>
        /// Performs a update to all subsystems returning the number of t-cycles
        /// taken.
        /// </summary>
        /// <returns>
        /// The total number of t-cycles taken by the step.
        /// </returns>
        public int Step()
        {
            //Log.Information("{0}",ToString());

            // Step 1: Check for interrupts
            var tCycles = CPU.CheckForInterrupts();

            // Step 2: Atomically run the next operation
            tCycles += CPU.Step();

            // Step 3: Run the DMA controller to move bytes directly into VRAM/OAM
            DMAController.Step(tCycles);

            // Step 4: Update the LCD subsystem to sync with the new number of cycles
            LCDDriver.Step(tCycles);

            // Step 5: Update the timer controller with the number of cycles
            Timer.Step(tCycles);

            // Step 6: Step audio subsystem
            APU.Step(tCycles);

            TCycles += tCycles;

            return tCycles; // Machine cycles translation
        }

        /// <summary>
        /// Called whenever a key down event is handled by the calling code
        /// </summary>
        /// <param name="key">The key pressed</param>
        public void HandleKeyDown(DeviceKey key)
        {
            JoypadHandler.Keydown(key);
        }

        /// <summary>
        /// Called whenever a key up event is handled by the calling code
        /// </summary>
        /// <param name="key">The key lifted</param>
        public void HandleKeyUp(DeviceKey key)
        {
            JoypadHandler.Keyup(key);
        }

        public override string ToString()
        {
            return $"{ControlRegisters} {CPU.Registers} {LCDRegisters} {Timer} {InterruptRegisters} Cyc:{Timer.SystemCounter}";
        }
    }
}
