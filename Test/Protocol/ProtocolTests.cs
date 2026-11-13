using Application.Protocol;
using Application.UseCases;
using Test.Support;

namespace Test.Protocol
{
    public class ChecksumTests
    {
        [Fact]
        public void UnknownCommandVector_IsFEE6()
        {
            Assert.Equal("FEE6", TankChecksum.Compute("\u0001ERR0"));
            Assert.Equal("\u0001ERR0FEE6\u0003", TankFrameBuilder.BuildUnknownCommand());
        }

        [Theory]
        [InlineData("\u0001INV000")]
        [InlineData("\u0001ENT000&&")]
        [InlineData("abc")]
        [InlineData("")]
        public void MatchesIndependentNaiveImplementation(string input)
        {
            Assert.Equal(NaiveChecksum.Of(input), TankChecksum.Compute(input));
        }

        [Fact]
        public void SumPlusChecksum_IsZeroModulo65536()
        {
            string text = "\u0001INV0002611120930&&";
            int sum = text.Sum(c => (int)c);
            int checksum = Convert.ToInt32(TankChecksum.Compute(text), 16);
            Assert.Equal(0, (sum + checksum) % 65536);
        }

        [Fact]
        public void Checksum_IsUppercaseHexOfFourChars()
        {
            string c = TankChecksum.Compute("\u0001INV000&&");
            Assert.Equal(4, c.Length);
            Assert.Equal(c.ToUpperInvariant(), c);
        }

        [Fact]
        public void Unwrap_AcceptsLowercaseChecksum_RejectsSpaces()
        {
            string frame = NaiveChecksum.Frame("INV0002611120930");
            string lower = frame[..^5] + frame[^5..^1].ToLowerInvariant() + frame[^1];
            Assert.Equal("INV000", TankFrame.Unwrap(lower).Header);

            string spaced = frame[..^5] + " " + frame[^4..^1] + frame[^1];
            var ex = Assert.Throws<TankFrameException>(() => TankFrame.Unwrap(spaced));
            Assert.Equal(TankFrameError.BadChecksumLength, ex.Error);
        }
    }

    public class TankFrameTests
    {
        private static readonly string Good = NaiveChecksum.Frame("INV0002611120930");

        [Fact]
        public void Unwrap_ReturnsHeaderAndBody()
        {
            var env = TankFrame.Unwrap(Good);
            Assert.Equal("INV000", env.Header);
            Assert.Equal("2611120930", env.Body);
        }

        [Fact]
        public void Unwrap_RejectsAlteredChecksum_BeforeReadingFields()
        {
            string altered = Good[..^2] + (Good[^2] == '0' ? '1' : '0') + Good[^1];
            var ex = Assert.Throws<TankFrameException>(() => TankFrame.Unwrap(altered));
            Assert.Equal(TankFrameError.ChecksumMismatch, ex.Error);
        }

        [Fact]
        public void Unwrap_RejectsAlteredPayload()
        {
            string altered = Good.Replace("0930", "0931");
            var ex = Assert.Throws<TankFrameException>(() => TankFrame.Unwrap(altered));
            Assert.Equal(TankFrameError.ChecksumMismatch, ex.Error);
        }

        [Theory]
        [InlineData("", TankFrameError.Empty)]
        [InlineData("INV000", TankFrameError.MissingSoh)]
        [InlineData("\u0001INV0002611120930&&ABCD", TankFrameError.MissingEtx)]
        [InlineData("\u0001INV0002611120930ABCD\u0003", TankFrameError.MissingTerminator)]
        [InlineData("\u0001INV0002611120930&&AB\u0003", TankFrameError.BadChecksumLength)]
        public void Unwrap_RejectsMalformedFrames(string raw, TankFrameError expected)
        {
            var ex = Assert.Throws<TankFrameException>(() => TankFrame.Unwrap(raw));
            Assert.Equal(expected, ex.Error);
        }

        [Fact]
        public void Unwrap_RecognizesErr0_AsUnknownCommand()
        {
            var ex = Assert.Throws<TankFrameException>(() => TankFrame.Unwrap("\u0001ERR0FEE6\u0003"));
            Assert.Equal(TankFrameError.UnknownCommand, ex.Error);
        }

        [Fact]
        public void Unwrap_Err0WithBadChecksum_IsMismatch()
        {
            var ex = Assert.Throws<TankFrameException>(() => TankFrame.Unwrap("\u0001ERR0FEE7\u0003"));
            Assert.Equal(TankFrameError.ChecksumMismatch, ex.Error);
        }

        [Fact]
        public void Unwrap_Truncated_AtEveryCutPoint_IsRejected()
        {
            // Cortar la trama en cualquier punto nunca debe producir un informe valido.
            string full = NaiveChecksum.Frame("INV0002611120930" + "01A000107" + string.Concat(Enumerable.Repeat(NaiveChecksum.Hex(1f), 7)));
            var parser = new ParseTankInventoryReport();
            for (int cut = 1; cut < full.Length; cut++)
            {
                string part = full[..cut];
                Assert.ThrowsAny<Exception>(() => parser.Execute(part));
            }
            Assert.Single(parser.Execute(full).Tanks!);
        }

        [Fact]
        public void Unwrap_TwoConcatenatedFrames_IsRejected()
        {
            string two = Good + Good;
            Assert.Throws<TankFrameException>(() => TankFrame.Unwrap(two));
        }
    }

    public class ParserTests
    {
        private static string InventoryBody(string tank, float[] floats) =>
            "INV000" + "2611120930" + tank + string.Concat(floats.Select(NaiveChecksum.Hex));

        [Fact]
        public void ParseInventory_ReadsExactValues()
        {
            string frame = NaiveChecksum.Frame("INV0002611120930" + "01A0001" + "07"
                + NaiveChecksum.Hex(1.0f) + NaiveChecksum.Hex(2.5f) + NaiveChecksum.Hex(3f) + NaiveChecksum.Hex(4f)
                + NaiveChecksum.Hex(5f) + NaiveChecksum.Hex(20.5f) + NaiveChecksum.Hex(7f)
                + "02B0000" + "07" + string.Concat(Enumerable.Repeat(NaiveChecksum.Hex(9f), 7)));

            var report = new ParseTankInventoryReport().Execute(frame);

            Assert.Equal(new DateTime(2026, 11, 12, 9, 30, 0), report.Date);
            Assert.Equal(2, report.Tanks!.Count);
            var t1 = report.Tanks[0];
            Assert.Equal(1, t1.NoTank);
            Assert.Equal('A', t1.CodeProduct);
            Assert.Equal(1, t1.StatusTank & TankStatusBits.DeliveryInProgress);
            Assert.Equal(1.0f, t1.TankData!.Volume);
            Assert.Equal(2.5f, t1.TankData.TCVolume);
            Assert.Equal(20.5f, t1.TankData.Temperature);
            Assert.Equal(9f, report.Tanks[1].TankData!.Volume);
        }

        [Fact]
        public void ParseInventory_3F800000_IsOne()
        {
            string frame = NaiveChecksum.Frame("INV0002611120930" + "01A000007" + string.Concat(Enumerable.Repeat("3F800000", 7)));
            Assert.Equal(1.0f, new ParseTankInventoryReport().Execute(frame).Tanks![0].TankData!.Volume);
        }

        [Theory]
        [InlineData("7FC00000")] // NaN
        [InlineData("7F800000")] // infinito
        public void ParseInventory_RejectsNonFiniteFloats(string hex)
        {
            string frame = NaiveChecksum.Frame("INV0002611120930" + "01A000007" + hex + string.Concat(Enumerable.Repeat("3F800000", 6)));
            var ex = Assert.Throws<TankFrameException>(() => new ParseTankInventoryReport().Execute(frame));
            Assert.Equal(TankFrameError.InvalidFloat, ex.Error);
        }

        [Theory]
        [InlineData("2613120930")] // mes 13
        [InlineData("2611122530")] // hora 25
        [InlineData("26111209AB")] // no numerica
        public void ParseInventory_RejectsImpossibleDates(string date)
        {
            string frame = NaiveChecksum.Frame("INV000" + date + "01A000007" + string.Concat(Enumerable.Repeat("3F800000", 7)));
            var ex = Assert.Throws<TankFrameException>(() => new ParseTankInventoryReport().Execute(frame));
            Assert.Equal(TankFrameError.InvalidDate, ex.Error);
        }

        [Fact]
        public void ParseDeliveries_ReturnsAllDeliveriesOfATank()
        {
            string one = "2611120800" + "2611120802" + "0A" + string.Concat(Enumerable.Range(1, 10).Select(i => NaiveChecksum.Hex(i * 10f)));
            string frame = NaiveChecksum.Frame("ENT0002611120930" + "01A03" + one + one + one);

            var report = new ParceDeliveryReport().Execute(frame);

            var tank = Assert.Single(report.Tanks!);
            Assert.Equal(3, tank.CountDeliveries);
            Assert.Equal(3, tank.Deliveries.Count);
            var d = tank.Deliveries[0];
            Assert.Equal(new DateTime(2026, 11, 12, 8, 0, 0), d.Start.Date);
            Assert.Equal(new DateTime(2026, 11, 12, 8, 2, 0), d.End.Date);
            Assert.Equal(10f, d.Start.Volume);
            Assert.Equal(50f, d.End.Volume);
            Assert.Equal(90f, d.Start.Height);
            Assert.Equal(100f, d.End.Height);
        }

        [Fact]
        public void ParseDeliveries_ChecksumAlteredIsRejected()
        {
            string frame = NaiveChecksum.Frame("ENT0002611120930" + "01A00");
            string altered = frame.Replace("ENT000", "ENT001");
            Assert.Throws<TankFrameException>(() => new ParceDeliveryReport().Execute(altered));
        }
    }

    public class BuilderTests
    {
        [Fact]
        public void BuildInventory_MatchesHandBuiltFrame()
        {
            var report = Frames.Inventory(new DateTime(2026, 11, 12, 9, 30, 0), (1, 'A', 1000f, 20f));
            string built = TankFrameBuilder.BuildInventory("INV000", report);

            string expected = NaiveChecksum.Frame("INV0002611120930" + "01A000007"
                + NaiveChecksum.Hex(1000f) + NaiveChecksum.Hex(1000f) + NaiveChecksum.Hex(100f) + NaiveChecksum.Hex(500f)
                + NaiveChecksum.Hex(1f) + NaiveChecksum.Hex(20f) + NaiveChecksum.Hex(5f));
            Assert.Equal(expected, built);
        }

        [Fact]
        public void RoundTrip_Inventory_IsExact()
        {
            var rng = new Infrastructure.Simulation.DeterministicRandom(42);
            var tanks = Enumerable.Range(1, 4)
                .Select(i => (i, (char)('A' + i % 3), (float)rng.NextRange(0, 40000), (float)rng.NextRange(5, 40)))
                .ToArray();
            var report = Frames.Inventory(new DateTime(2026, 1, 31, 23, 59, 0), tanks);

            var parsed = new ParseTankInventoryReport().Execute(TankFrameBuilder.BuildInventory("INV000", report));

            Assert.Equal(report.Date, parsed.Date);
            for (int i = 0; i < 4; i++)
            {
                Assert.Equal(report.Tanks![i].NoTank, parsed.Tanks![i].NoTank);
                Assert.Equal(report.Tanks[i].CodeProduct, parsed.Tanks[i].CodeProduct);
                Assert.Equal(report.Tanks[i].TankData!.Volume, parsed.Tanks[i].TankData!.Volume);
                Assert.Equal(report.Tanks[i].TankData!.Temperature, parsed.Tanks[i].TankData!.Temperature);
            }
        }

        [Fact]
        public void RoundTrip_Deliveries_IsExact()
        {
            var d1 = Frames.Delivery(new DateTime(2026, 5, 1, 10, 0, 0), 1000.5f, 6000.25f);
            var d2 = Frames.Delivery(new DateTime(2026, 5, 2, 11, 30, 0), 2000f, 7000f);
            var report = Frames.Deliveries(new DateTime(2026, 5, 3, 0, 0, 0), 2, d1, d2);

            var parsed = new ParceDeliveryReport().Execute(TankFrameBuilder.BuildDeliveries("ENT000", report));

            var tank = Assert.Single(parsed.Tanks!);
            Assert.Equal(2, tank.Deliveries.Count);
            Assert.Equal(d1.Start.Volume, tank.Deliveries[0].Start.Volume);
            Assert.Equal(d1.End.Volume, tank.Deliveries[0].End.Volume);
            Assert.Equal(d2.Start.Date, tank.Deliveries[1].Start.Date);
            Assert.Equal(d2.End.Height, tank.Deliveries[1].End.Height);
        }

        [Fact]
        public void BuiltFrames_AlwaysPassTheNaiveChecksum()
        {
            string frame = TankFrameBuilder.BuildInventory("INV000", Frames.Inventory(DateTime.Today, (1, 'C', 1f, 1f)));
            string head = frame[..^5];
            Assert.Equal(NaiveChecksum.Of(head), frame[^5..^1]);
        }

        [Fact]
        public void EmptyDeliveries_AreValid()
        {
            var report = new Domain.Entities.DeliveryTankReport
            {
                Date = new DateTime(2026, 2, 2, 2, 2, 0),
                Tanks = [new Domain.Entities.DeliveryTank { NoTank = 1, CodeProduct = 'A', CountDeliveries = 0, Deliveries = [] }],
            };
            var parsed = new ParceDeliveryReport().Execute(TankFrameBuilder.BuildDeliveries("ENT000", report));
            Assert.Empty(parsed.Tanks![0].Deliveries);
        }
    }
}
