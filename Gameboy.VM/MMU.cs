﻿using System;
using System.Diagnostics;
using Gameboy.VM.Interrupts;
using Gameboy.VM.LCD;
using Gameboy.VM.Sound;

namespace Gameboy.VM
{
    internal class MMU
    {
        private const int WRAMSize = 0x2000;
        private const int HRAMSize = 0x7F;
        private const int VRAMSize = 0x1FFFF;
        private const int OAMRAMSize = 0xA0;
        private const int WaveRAMSize = 0x10;

        private readonly byte[] _rom = new byte[0x100];
        private readonly ControlRegisters _controlRegisters;
        private readonly SoundRegisters _soundRegisters;
        private readonly LCDRegisters _lcdRegisters;
        private readonly InterruptRegisters _interruptRegisters;
        private readonly Cartridge.Cartridge _cartridge;

        private readonly byte[] _workingRam = new byte[WRAMSize];
        private readonly byte[] _hRam = new byte[HRAMSize];
        private readonly byte[] _vRam = new byte[VRAMSize]; // TODO - Implement CGB vram banks
        private readonly byte[] _oamRam = new byte[OAMRAMSize];
        private readonly byte[] _waveRam = new byte[WaveRAMSize];

        public MMU(byte[] rom, ControlRegisters controlRegisters, SoundRegisters soundRegisters, LCDRegisters lcdRegisters, InterruptRegisters interruptRegisters, Cartridge.Cartridge cartridge)
        {
            Array.Clear(_rom, 0, _rom.Length);
            Array.Copy(rom, 0, _rom, 0, rom.Length);
            _controlRegisters = controlRegisters;
            _soundRegisters = soundRegisters;
            _lcdRegisters = lcdRegisters;
            _interruptRegisters = interruptRegisters;
            _cartridge = cartridge;
        }

        internal void Clear()
        {
            Array.Clear(_workingRam, 0, _workingRam.Length);
            Array.Clear(_hRam, 0, _hRam.Length);
            Array.Clear(_vRam, 0, _vRam.Length);
            Array.Clear(_oamRam, 0, _oamRam.Length);
            Array.Clear(_waveRam, 0, _waveRam.Length);
        }

        internal byte ReadByte(ushort address)
        {
            Trace.TraceInformation($"Reading from {address:X4}");

            if (address <= 0xFF)
                return _controlRegisters.RomDisabledRegister > 0
                    ? _rom[address]                     // Read from device ROM if in that state
                    : _cartridge.ReadRom(address);      // Read from the 8kB ROM on the cartridge
            if (address >= 0x0100 && address <= 0x7FFF) // Read from the 8kB ROM on the cartridge
                return _cartridge.ReadRom(address);
            if (address >= 0x8000 && address <= 0x9FFF) // Read from the 8kB Video RAM
            {
                // Video RAM is unreadable during STAT mode 3
                if (_lcdRegisters.StatMode == StatMode.TransferringDataToDriver)
                {
                    Trace.TraceWarning("Attempted to read VRAM ({0:X2}) during stat mode 3", address);
                    return 0xFF; // May not be 100% correct, based on speculative information on the internet
                }

                return _vRam[address - 0x8000];
            }
            if (address >= 0xA000 && address <= 0xBFFF) // Read from MBC RAM on the cartridge
                return _cartridge.ReadRam(address);
            if (address >= 0xC000 && address <= 0xDFFF) // Read from 8kB internal RAM
                return _workingRam[address - 0xC000];
            if (address >= 0xE000 && address <= 0xFDFF) // Read from echo of internal RAM
                return _workingRam[address - 0xE000];
            if (address >= 0xFE00 && address <= 0xFE9F) // Read from sprite attribute table
            {
                // OAM RAM is unreadable during STAT mode 2 & 3                                              
                if (_lcdRegisters.StatMode == StatMode.OAMRAMPeriod || _lcdRegisters.StatMode == StatMode.TransferringDataToDriver)
                {
                    Trace.TraceWarning("Attempted to read OAM RAM ({0:X2}) during stat mode {1}", address, _lcdRegisters.StatMode);
                    return 0xFF; // May not be 100% correct, based on speculative information on the internet
                }

                return _oamRam[address - 0xFE00];
            }
            if (address >= 0xFEA0 && address <= 0xFEFF) // Unusable addresses
                return ReadUnusedAddress(address);
            if (address == 0xFF00) // P1 Register - TODO
            {
                Trace.TraceWarning("Port (P1) register not yet implemented");
                return 0x0;
            }
            if (address == 0xFF01) // SB register
                return _controlRegisters.SerialTransferData;
            if (address == 0xFF02) // SC register
                return _controlRegisters.SerialTransferControl;
            if (address == 0xFF03) // Unused address - all reads return 0
                return ReadUnusedAddress(address);
            if (address == 0xFF04) // Divider
                return _controlRegisters.Divider;
            if (address == 0xFF05) // Timer Counter
                return _controlRegisters.TimerCounter;
            if (address == 0xFF06) // Timer Modulo
                return _controlRegisters.TimerModulo;
            if (address == 0xFF07) // TAC Register
                return _controlRegisters.TimerController;
            if (address >= 0xFF08 && address <= 0xFF0E) // Unused addresses - all reads return 0
                return ReadUnusedAddress(address);
            if (address == 0xFF0F) // IF Register
                return _interruptRegisters.InterruptRequest;
            if (address >= 0xFF10 && address <= 0xFF26) // Sound registers
                return _soundRegisters.ReadFromRegister(address);
            if (address >= 0xFF27 && address <= 0xFF2F) // Unused addresses - all reads return 0
                return ReadUnusedAddress(address);
            if (address >= 0xFF30 && address <= 0xFF3F) // Wave Pattern RAM
                return _waveRam[address - 0xFF30];
            if (address == 0xFF40) // LCDC Register
                return _lcdRegisters.LCDControlRegister;
            if (address == 0xFF41) // STAT Register
                return _lcdRegisters.StatRegister;
            if (address == 0xFF42) // SCY Register
                return _lcdRegisters.ScrollY;
            if (address == 0xFF43) // SCX Register
                return _lcdRegisters.ScrollX;
            if (address == 0xFF44) // LY Register
                return _lcdRegisters.LCDCurrentScanline;
            if (address == 0xFF45) // LYC Register
                return _lcdRegisters.LYCompare;
            if (address == 0xFF46) // DMA Register - TODO
            {
                Trace.TraceWarning("DMA register read, not yet implemented");
                return 0x0;
            }
            if (address == 0xFF47) // Background Palette Register
                return _lcdRegisters.BackgroundPaletteData;
            if (address == 0xFF48) // Object 0 Palette Register
                return _lcdRegisters.ObjectPaletteData0;
            if (address == 0xFF49) // Object 1 Palette Register
                return _lcdRegisters.ObjectPaletteData1;
            if (address == 0xFF4A) // WY Register
                return _lcdRegisters.WindowY;
            if (address == 0xFF4B) // WX Register
                return _lcdRegisters.WindowX;
            if (address >= 0xFF4C && address <= 0xFF4F) // Unused addresses (TODO 0xFF4D used in CGB)
                return ReadUnusedAddress(address);
            if (address == 0xFF50) // Is device ROM enabled?
                return _controlRegisters.RomDisabledRegister;
            if (address >= 0xFF51 && address <= 0xFF7F) // Unused addresses (TODO some used in CGB)
                return ReadUnusedAddress(address);
            if (address >= 0xFF80 && address <= 0xFFFE) // Read from HRAM
                return _hRam[address - 0xFF80];
            if (address == 0xFFFF) // Read from the interrupt enable register
                return _interruptRegisters.InterruptEnable;

            throw new Exception($"Memory address {address:X4} doesn't map to anything");
        }

        private static byte ReadUnusedAddress(ushort address)
        {
            Trace.TraceWarning("Attempt to read from unused memory location {0:X4}", address);
            return 0x0;
        }

        internal ushort ReadWord(ushort address) =>
            (ushort)(ReadByte(address) | (ReadByte((ushort)((address + 1) & 0xFFFF)) << 8));

        /// <summary>
        /// 
        /// </summary>
        /// <param name="address"></param>
        /// <param name="value"></param>
        /// <returns>The number of cpu cycles taken to write</returns>
        internal int WriteByte(ushort address, byte value)
        {
            Trace.TraceInformation($"Writing {value:X2} to {address:X4}");

            if (address <= 0x7FFF) // Write to the 8kB ROM on the cartridge
                _cartridge.WriteRom(address, value);
            else if (address >= 0x8000 && address <= 0x9FFF) // Write to the 8kB Video RAM
                _vRam[address - 0x8000] = value;
            else if (address >= 0xA000 && address <= 0xBFFF) // Write to the MBC RAM on the cartridge - TODO
                _cartridge.WriteRam(address, value);
            else if (address >= 0xC000 && address <= 0xDFFF) // Write to the 8kB internal RAM
                _workingRam[address - 0xC000] = value;
            else if (address >= 0xE000 && address <= 0xFDFF) // Write to the 8kB internal RAM
                _workingRam[address - 0xE000] = value;
            else if (address >= 0xFE00 && address <= 0xFE9F) // Write to the sprite attribute table
                _oamRam[address - 0xFE00] = value;
            else if (address >= 0xFEA0 && address <= 0xFEFF) // Unusable addresses - writes explicitly ignored
                Trace.TraceWarning("Unusable address {0:X4} for write", address);
            else if (address == 0xFF00) // IO Ports Register - TODO
                Trace.TraceWarning("IO Ports register is not implemented yet");
            else if (address == 0xFF01)
                _controlRegisters.SerialTransferData = value;
            else if (address == 0xFF02)
                _controlRegisters.SerialTransferControl = value;
            else if (address == 0xFF03)
                Trace.TraceWarning("Write to unused address 0xFF03, no operation performed");
            else if (address == 0xFF04)
                _controlRegisters.Divider = 0x0; // Always reset divider to 0 on write
            else if (address == 0xFF05)
                _controlRegisters.TimerCounter = value;
            else if (address == 0xFF06)
                _controlRegisters.TimerModulo = value;
            else if (address == 0xFF07)
                _controlRegisters.TimerController = value;
            else if (address >= 0xFF08 && address <= 0xFF0E) // Unused addresses
                Trace.TraceWarning("Write to unused address {0:X4}", address);
            else if (address == 0xFF0F)
                _interruptRegisters.InterruptRequest = value;
            else if (address >= 0xFF10 && address <= 0xFF26)
                _soundRegisters.WriteToRegister(address, value);
            else if (address >= 0xFF27 && address <= 0xFF2F) // Unused addresses
                Trace.TraceWarning("Write to unused address {0:X4}", address);
            else if (address >= 0xFF30 && address <= 0xFF3F) // Waveform RAM
                _waveRam[address - 0xFF30] = value;
            else if (address == 0xFF40)
                _lcdRegisters.LCDControlRegister = value;
            else if (address == 0xFF41)
                _lcdRegisters.StatRegister = value;
            else if (address == 0xFF42)
                _lcdRegisters.ScrollY = value;
            else if (address == 0xFF43)
                _lcdRegisters.ScrollX = value;
            else if (address == 0xFF44)
                Trace.TraceWarning("Can't write directly to LY register from MMU");
            else if (address == 0xFF45)
                _lcdRegisters.LYCompare = value;
            else if (address == 0xFF46) // DMA register - TODO
                Trace.TraceWarning("DMA register not yet implemented");
            else if (address == 0xFF47)
                _lcdRegisters.BackgroundPaletteData = value;
            else if (address == 0xFF48)
                _lcdRegisters.ObjectPaletteData0 = value;
            else if (address == 0xFF49)
                _lcdRegisters.ObjectPaletteData1 = value;
            else if (address == 0xFF4A)
                _lcdRegisters.WindowY = value;
            else if (address == 0xFF4B)
                _lcdRegisters.WindowX = value;
            else if (address >= 0xFF4C && address <= 0xFF4F) // Unused addresses (TODO 0xFF4D used in CGB)
                Trace.TraceWarning("Write to unused address {0:X4}", address);
            else if (address == 0xFF50) // Undocumented register to unmap ROM and map cartridge
                _controlRegisters.RomDisabledRegister = value;
            else if (address >= 0xFF51 && address <= 0xFF7F) // Unused addresses (TODO - some used in CGB)
                Trace.TraceWarning("Write to unused address {0:X4}", address);
            else if (address >= 0xFF80 && address <= 0xFFFE)  // Write to HRAM
                _hRam[address - 0xFF80] = value;
            else if (address == 0xFFFF) // Write to interrupt enable register
                _interruptRegisters.InterruptEnable = value;
            else
                // Happy to throw an exception and crash here as we should map all addresses
                throw new Exception($"Address {address:X4} is not mapped");

            return 2;
        }

        /// <summary>
        /// Write a 2 byte value into the specified memory address.
        /// </summary>
        /// <param name="address"></param>
        /// <param name="value"></param>
        /// <returns>The corresponding number of CPU cycles (4).</returns>
        internal int WriteWord(ushort address, in ushort value)
        {
            return
                WriteByte(address, (byte)(value & 0xFF)) +
                WriteByte((ushort)((address + 1) & 0xFFFF), (byte)(value >> 8));
        }
    }
}