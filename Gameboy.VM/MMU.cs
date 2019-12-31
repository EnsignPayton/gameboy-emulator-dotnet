﻿using System;
using System.Diagnostics;

namespace Gameboy.VM
{
    internal class MMU
    {
        private const int WRAMSize = 0x1FFF;
        private const int HRAMSize = 0x7E;

        private readonly byte[] _rom;
        private readonly ControlRegisters _controlRegisters;
        private readonly Cartridge _cartridge;

        private readonly byte[] _workingRam = new byte[WRAMSize];
        private readonly byte[] _hRam = new byte[HRAMSize];

        public MMU(byte[] rom, ControlRegisters controlRegisters, Cartridge cartridge)
        {
            _rom = rom;
            _controlRegisters = controlRegisters;
            _cartridge = cartridge;
        }

        internal void Clear()
        {
            Array.Clear(_workingRam, 0, _workingRam.Length);
            Array.Clear(_hRam, 0, _hRam.Length);
        }

        internal byte ReadByte(ushort address)
        {
            Trace.WriteLine($"Reading from {address:X4}");

            return address switch
            {
                // ReSharper disable once ConditionIsAlwaysTrueOrFalse
                _ when address <= 0x0100 && _controlRegisters.RomDisabledRegister > 0 => _rom[address], // Read from device ROM if in that state
                _ when address <= 0x0100 && _controlRegisters.RomDisabledRegister == 0 => _cartridge.ReadByte(address),  // Read from the 8kB ROM on the cartridge
                _ when address >= 0x0100 && address <= 0x7FFF => _cartridge.ReadByte(address), // Read from the 8kB ROM on the cartridge
                _ when address >= 0x8000 && address <= 0x9FFF => 0x0, // Read from the 8kB Video RAM - TODO
                _ when address >= 0xA000 && address <= 0xBFFF => 0x0, // Read from MBC RAM on the cartridge - TODO
                _ when address >= 0xC000 && address <= 0xDFFF => _workingRam[address - 0xC000], // Read from 8kB internal RAM
                _ when address >= 0xE000 && address <= 0xFDFF => _workingRam[address - 0xE000], // Read from echo of internal RAM
                _ when address >= 0xFE00 && address <= 0xFE9F => 0x0, // Read from sprite attribute table - TODO
                _ when address >= 0xFEA0 && address <= 0xFEFF => 0x0, // Unusable addresses
                _ when address == 0xFF50 => _controlRegisters.RomDisabledRegister, // Is device ROM enabled?
                _ when address >= 0xFF00 && address <= 0xFF7F => 0x0, // I/O Ports - TODO
                _ when address >= 0xFF80 && address <= 0xFFFE => _hRam[address - 0xFF80], // Read from HRAM
                _ when address == 0xFFFF => 0x0, // Read from the interrupt enable register - TODO
                _ => throw new NotImplementedException($"Memory address {address} doesn't map to anything"),
            };
        }

        internal ushort ReadWord(ushort address) => (ushort)((ReadByte(address) << 8) | (ReadByte(address)));

        /// <summary>
        /// 
        /// </summary>
        /// <param name="address"></param>
        /// <param name="value"></param>
        /// <returns>The number of cpu cycles taken to write</returns>
        internal int WriteByte(ushort address, byte value)
        {
            Trace.WriteLine($"Writing {value:X2} to {address:X4}");
            switch(address)
            {
                // ReSharper disable once ConditionIsAlwaysTrueOrFalse
                case var a when address >= 0x0000 && address <= 0x7FFF:
                    // Write to the 8kB ROM on the cartridge - TODO
                    break;
                case var a when address >= 0x8000 && address <= 0x9FFF:
                    // Write to the 8kB Video RAM - TODO
                    break;
                case var a when address >= 0xA000 && address <= 0xBFFF:
                    // Write to the MBC RAM on the cartridge - TODO
                    break;
                case var a when address >= 0xC000 && address <= 0xDFFF:
                    // Write to the 8kB internal RAM
                    _workingRam[address - 0xC000] = value;
                    break;
                case var a when address >= 0xE000 && address <= 0xFDFF:
                    // Write to the 8kB internal RAM
                    _workingRam[address - 0xE000] = value;
                    break;
                case var a when address >= 0xFE00 && address <= 0xFE9F:
                    // Write to the sprite attribute table - TODO
                    break;
                case var a when address >= 0xFEA0 && address <= 0xFEFF:
                    // Unusable addresses
                    break;
                case var a when address == 0xFF50: // Is ROM disabled control register
                    _controlRegisters.RomDisabledRegister = value;
                    break;
                case var a when address >= 0xFF00 && address <= 0xFF7F:
                    // I/O Ports - TODO
                    break;
                case var a when address >= 0xFF80 && address <= 0xFFFE:
                    // Write to HRAM
                    _hRam[a - 0xFF80] = value;
                    break;
                case 0xFFFF:
                    // Write to interrupt enable register - TODO
                    break;
            }

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
