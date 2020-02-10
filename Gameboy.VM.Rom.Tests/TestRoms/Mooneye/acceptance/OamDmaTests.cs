﻿using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace Gameboy.VM.Rom.Tests.TestRoms.Mooneye.acceptance
{
    public class OamDmaTests
    {
        [Fact]
        public async Task MooneyeBasicOamDmaTest()
        {
            var expectedFrameBuffer = await File.ReadAllLinesAsync(
                Path.Join(TestUtils.SolutionDirectory, "Roms", "tests", "mooneye-gb", "acceptance", "oam_dma", "framebuffer"));
            await TestUtils.TestRomAgainstResult(
                Path.Join("Roms", "tests", "mooneye-gb", "acceptance", "oam_dma", "basic.gb"), expectedFrameBuffer, 1000 * 10, 0x486E);
        }

        [Fact]
        public async Task MooneyeBasicRegReadTests()
        {
            var expectedFrameBuffer = await File.ReadAllLinesAsync(
                Path.Join(TestUtils.SolutionDirectory, "Roms", "tests", "mooneye-gb", "acceptance", "oam_dma", "framebuffer"));
            await TestUtils.TestRomAgainstResult(
                Path.Join("Roms", "tests", "mooneye-gb", "acceptance", "oam_dma", "reg_read.gb"), expectedFrameBuffer, 1000 * 10, 0x486E);
        }

        [Fact(Skip = "TODO - Known failure - unclear how important as fails on CGB device")]
        public async Task MooneyeBasicOamDmaSourcesGs()
        {
            var expectedFrameBuffer = await File.ReadAllLinesAsync(
                Path.Join(TestUtils.SolutionDirectory, "Roms", "tests", "mooneye-gb", "acceptance", "oam_dma", "framebuffer"));
            await TestUtils.TestRomAgainstResult(
                Path.Join("Roms", "tests", "mooneye-gb", "acceptance", "oam_dma", "sources-GS.gb"), expectedFrameBuffer, 1000 * 10, 0x490E);
        }
    }
}
