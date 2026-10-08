// Copyright 2026 The Noda Time Authors. All rights reserved.
// Use of this source code is governed by the Apache License 2.0,
// as found in the LICENSE.txt file.

using System;
using System.IO;
using System.Text;
using NodaTime.Testing;
using NodaTime.Text;
using NUnit.Framework;

namespace NodaTime.Test.Text
{
    /// <summary>
    /// Tests for the interval endpoint defaulting support: the three-state endpoint exchange
    /// (missing / explicitly infinite / concrete instant), caller-supplied missing-endpoint policies
    /// (no implicit clock reads), reversed-interval rejection with the original text position, and the
    /// default <see cref="IntervalPattern.ExtendedIso"/> behaviour.
    /// </summary>
    public class IntervalPatternTest
    {
        private static readonly Instant Start = Instant.FromUtc(2026, 10, 8, 11, 30, 0);
        private static readonly Instant End = Instant.FromUtc(2026, 10, 8, 12, 45, 0);
        private static readonly Instant FixedMissing = Instant.FromUtc(1970, 1, 1, 0, 0);

        private static string Iso(Instant instant) => InstantPattern.ExtendedIso.Format(instant);

        /// <summary>A counting clock used to prove that parsing never consults a clock on its own.</summary>
        private sealed class CountingClock : IClock
        {
            public int ReadCount { get; private set; }
            public Instant GetCurrentInstant()
            {
                ReadCount++;
                return Instant.FromUtc(2026, 10, 8, 0, 0);
            }
        }

        // ----------------------------------------------------------------------
        // The four exchange objects: finite, infinite, missing and reversed.
        // ----------------------------------------------------------------------

        [Test]
        public void Roundtrip_Finite_EndpointsTriState()
        {
            var endpoints = new IntervalEndpoints(new IntervalEndpoint(Start), new IntervalEndpoint(End));
            string text = IntervalPattern.ExtendedIso.Format(endpoints);
            Assert.AreEqual($"{Iso(Start)}/{Iso(End)}", text);

            var parsed = IntervalPattern.ExtendedIso.ParseEndpoints(text).GetValueOrThrow();
            Assert.AreEqual(endpoints, parsed);
            Assert.AreEqual(IntervalEndpointState.Instant, parsed.Start.State);
            Assert.AreEqual(IntervalEndpointState.Instant, parsed.End.State);
            Assert.AreEqual(Start, parsed.Start.Instant);
            Assert.AreEqual(End, parsed.End.Instant);

            var interval = IntervalPattern.ExtendedIso.Parse(text).GetValueOrThrow();
            Assert.AreEqual(Start, interval.Start);
            Assert.AreEqual(End, interval.End);
            Assert.IsTrue(interval.HasStart);
            Assert.IsTrue(interval.HasEnd);
        }

        [Test]
        public void Roundtrip_Infinite_EndpointsTriState()
        {
            var endpoints = new IntervalEndpoints(IntervalEndpoint.Infinite, IntervalEndpoint.Infinite);
            string text = IntervalPattern.ExtendedIso.Format(endpoints);
            Assert.AreEqual("StartOfTime/EndOfTime", text);

            var parsed = IntervalPattern.ExtendedIso.ParseEndpoints(text).GetValueOrThrow();
            Assert.AreEqual(endpoints, parsed);
            Assert.AreEqual(IntervalEndpointState.Infinite, parsed.Start.State);
            Assert.AreEqual(IntervalEndpointState.Infinite, parsed.End.State);
            Assert.IsFalse(parsed.Start.IsMissing);
            Assert.IsTrue(parsed.Start.IsInfinite);
            Assert.IsFalse(parsed.End.IsMissing);
            Assert.IsTrue(parsed.End.IsInfinite);

            var interval = IntervalPattern.ExtendedIso.Parse(text).GetValueOrThrow();
            Assert.IsFalse(interval.HasStart);
            Assert.IsFalse(interval.HasEnd);
            Assert.Throws<InvalidOperationException>(() => { var _ = interval.Start; });
            Assert.Throws<InvalidOperationException>(() => { var _ = interval.End; });
        }

        [Test]
        public void Roundtrip_Missing_EndpointsTriState_AndPolicyResolution()
        {
            var endpoints = new IntervalEndpoints(IntervalEndpoint.Missing, IntervalEndpoint.Missing);
            string text = IntervalPattern.ExtendedIso.Format(endpoints);
            Assert.AreEqual("/", text);

            // The raw tri-state endpoints round-trip.
            var parsed = IntervalPattern.ExtendedIso.ParseEndpoints(text).GetValueOrThrow();
            Assert.AreEqual(endpoints, parsed);
            Assert.AreEqual(IntervalEndpointState.Missing, parsed.Start.State);
            Assert.AreEqual(IntervalEndpointState.Missing, parsed.End.State);
            Assert.IsTrue(parsed.Start.IsMissing);
            Assert.IsTrue(parsed.End.IsMissing);
            Assert.Throws<InvalidOperationException>(() => { var _ = parsed.Start.Instant; });

            // The default pattern rejects, rather than inventing a value or reading a clock.
            var rejected = IntervalPattern.ExtendedIso.Parse(text);
            Assert.IsFalse(rejected.Success);
            var rejectedException = (UnparsableValueException) rejected.Exception;
            Assert.AreEqual(text, rejectedException.Value);
            StringAssert.StartsWith("The start endpoint is missing.", rejectedException.Message);

            // Explicit policies, all clock-free except where the caller itself reads a clock.
            var infinite = IntervalPattern.Create(MissingEndpointPolicy.TreatAsInfinite).Parse(text).GetValueOrThrow();
            Assert.IsFalse(infinite.HasStart);
            Assert.IsFalse(infinite.HasEnd);

            var fixedPattern = IntervalPattern.Create(MissingEndpointPolicy.Use(FixedMissing));
            var fixedInterval = fixedPattern.Parse(text).GetValueOrThrow();
            Assert.AreEqual(FixedMissing, fixedInterval.Start);
            Assert.AreEqual(FixedMissing, fixedInterval.End);
        }

        [Test]
        public void Missing_OneSide_EachPolicyAppliesPerSide()
        {
            string missingStart = $"/{Iso(End)}";
            var pattern = IntervalPattern.Create(
                MissingEndpointPolicy.Use(FixedMissing), MissingEndpointPolicy.Reject);
            var interval = pattern.Parse(missingStart).GetValueOrThrow();
            Assert.AreEqual(FixedMissing, interval.Start);
            Assert.AreEqual(End, interval.End);

            string missingEnd = $"{Iso(Start)}/";
            Assert.IsFalse(pattern.Parse(missingEnd).Success, "end policy is Reject");

            // A fixed end must not be earlier than the concrete start; use an instant after Start.
            var both = IntervalPattern.Create(
                MissingEndpointPolicy.TreatAsInfinite, MissingEndpointPolicy.Use(End));
            var mixed = both.Parse(missingEnd).GetValueOrThrow();
            Assert.IsTrue(mixed.HasStart);
            Assert.AreEqual(Start, mixed.Start);
            Assert.AreEqual(End, mixed.End);

            // Missing start -> infinite; concrete end.
            var infiniteStart = both.Parse("/" + Iso(End)).GetValueOrThrow();
            Assert.IsFalse(infiniteStart.HasStart);
            Assert.AreEqual(End, infiniteStart.End);
        }

        [Test]
        public void Reversed_RejectedWithOriginalTextPosition()
        {
            // End (segment after '/') is earlier than start. The error must point at the end segment.
            string text = $"{Iso(End)}/{Iso(Start)}";
            int expectedIndex = Iso(End).Length + 1; // index of the first character of the end segment

            var result = IntervalPattern.ExtendedIso.Parse(text);
            Assert.IsFalse(result.Success);
            var exception = (UnparsableValueException) result.Exception;
            Assert.AreEqual(text, exception.Value);
            Assert.AreEqual(expectedIndex, exception.Index);
            StringAssert.StartsWith("The interval end is earlier than the interval start.", exception.Message);
            Assert.Throws<UnparsableValueException>(() => result.GetValueOrThrow());

            // ParseEndpoints itself does not order-check; converting does (Interval ctor unchanged behaviour).
            var endpoints = IntervalPattern.ExtendedIso.ParseEndpoints(text).GetValueOrThrow();
            Assert.AreEqual(new IntervalEndpoint(End), endpoints.Start);
            Assert.AreEqual(new IntervalEndpoint(Start), endpoints.End);
            Assert.Throws<ArgumentOutOfRangeException>(
                () => endpoints.ToInterval(MissingEndpointPolicy.TreatAsInfinite, MissingEndpointPolicy.TreatAsInfinite));
        }

        [Test]
        public void StructuralErrors_PreservePosition()
        {
            // No separator: error at end of input.
            var noSeparator = IntervalPattern.ExtendedIso.Parse(Iso(Start));
            Assert.IsFalse(noSeparator.Success);
            var noSeparatorException = (UnparsableValueException) noSeparator.Exception;
            Assert.AreEqual(Iso(Start).Length, noSeparatorException.Index);

            // Two separators: error at the second '/'.
            string twoSeparators = $"{Iso(Start)}//";
            var two = IntervalPattern.ExtendedIso.Parse(twoSeparators);
            Assert.IsFalse(two.Success);
            Assert.AreEqual(Iso(Start).Length + 1, ((UnparsableValueException) two.Exception).Index);

            // Bad instant within the end: error offset points inside the original text.
            string badEnd = $"{Iso(Start)}/not-an-instant";
            var bad = IntervalPattern.ExtendedIso.Parse(badEnd);
            Assert.IsFalse(bad.Success);
            var badException = (UnparsableValueException) bad.Exception;
            Assert.AreEqual(Iso(Start).Length + 1, badException.Index);

            // Empty and null.
            Assert.IsFalse(IntervalPattern.ExtendedIso.Parse("").Success);
            Assert.Throws<ArgumentNullException>(() => IntervalPattern.ExtendedIso.Parse(null!).GetValueOrThrow());
        }

        // ----------------------------------------------------------------------
        // Zero clock reads: neither the default pattern nor fixed/infinite
        // policies consult any clock. A caller policy may read a clock explicitly.
        // ----------------------------------------------------------------------

        [Test]
        public void NoClockIsReadByBuiltInParsing()
        {
            var clock = new CountingClock();

            // Exercise every built-in path (including failures and reversed input) repeatedly.
            for (int i = 0; i < 5; i++)
            {
                IntervalPattern.ExtendedIso.Parse($"{Iso(Start)}/{Iso(End)}");
                IntervalPattern.ExtendedIso.Parse("StartOfTime/EndOfTime");
                IntervalPattern.ExtendedIso.Parse("/");
                IntervalPattern.ExtendedIso.Parse($"{Iso(End)}/{Iso(Start)}");
                IntervalPattern.Create(MissingEndpointPolicy.TreatAsInfinite).Parse("/");
                IntervalPattern.Create(MissingEndpointPolicy.Use(FixedMissing)).Parse($"/{Iso(End)}");
            }

            // The clock is wired only to an explicitly caller-controlled policy, and must be the only reader.
            var explicitNow = clock.GetCurrentInstant();
            var callerPolicy = MissingEndpointPolicy.Resolve(isStart => isStart ? clock.GetCurrentInstant() : (Instant?) null);
            var interval = IntervalPattern.Create(callerPolicy).Parse("/").GetValueOrThrow();
            Assert.AreEqual(explicitNow, interval.Start);
            Assert.IsFalse(interval.HasEnd);
            Assert.AreEqual(2, clock.ReadCount, "only the explicit caller reads the clock (once above, once in parse)");
        }

        [Test]
        public void ThrowingResolver_IsReportedNotThrown()
        {
            var policy = MissingEndpointPolicy.Resolve(_ => throw new InvalidOperationException("no value available"));
            var result = IntervalPattern.Create(policy).Parse("/");
            Assert.IsFalse(result.Success);
            var exception = (UnparsableValueException) result.Exception;
            Assert.AreEqual("/", exception.Value);
            Assert.IsInstanceOf<InvalidOperationException>(exception.InnerException);
            Assert.Throws<UnparsableValueException>(() => result.GetValueOrThrow());
        }

        // ----------------------------------------------------------------------
        // Default interface regression: formatting matches Interval.ToString(),
        // and normal finite/infinite intervals round-trip as before.
        // ----------------------------------------------------------------------

        [Test]
        public void DefaultInterface_FormatMatchesIntervalToString()
        {
            var finite = new Interval(Start, End);
            Assert.AreEqual(finite.ToString(), IntervalPattern.ExtendedIso.Format(finite));
            Assert.AreEqual($"{Iso(Start)}/{Iso(End)}", IntervalPattern.ExtendedIso.Format(finite));

            var infinite = new Interval(null, null);
            Assert.AreEqual("StartOfTime/EndOfTime", IntervalPattern.ExtendedIso.Format(infinite));
            Assert.AreEqual(infinite.ToString(), IntervalPattern.ExtendedIso.Format(infinite));

            var halfOpen = new Interval(Start, null);
            Assert.AreEqual(halfOpen.ToString(), IntervalPattern.ExtendedIso.Format(halfOpen));
        }

        [Test]
        public void DefaultInterface_ExistingValuesStillRoundTrip()
        {
            var cases = new[]
            {
                new Interval(Start, End),
                new Interval(Start, Start),
                new Interval(null, End),
                new Interval(Start, null),
                new Interval(null, null),
            };
            foreach (var expected in cases)
            {
                string text = IntervalPattern.ExtendedIso.Format(expected);
                var actual = IntervalPattern.ExtendedIso.Parse(text).GetValueOrThrow();
                Assert.AreEqual(expected, actual, text);
            }
        }

        [Test]
        public void EndpointEquality_ThreeStatesAreDistinct()
        {
            TestHelper.TestEqualsStruct(
                new IntervalEndpoint(Start),
                new IntervalEndpoint(Start),
                new IntervalEndpoint(End),
                IntervalEndpoint.Missing,
                IntervalEndpoint.Infinite);
            Assert.AreNotEqual(IntervalEndpoint.Missing, IntervalEndpoint.Infinite);
            Assert.AreEqual(new IntervalEndpoints(IntervalEndpoint.Missing, new IntervalEndpoint(End)),
                            new IntervalEndpoints(IntervalEndpoint.Missing, new IntervalEndpoint(End)));
            Assert.AreNotEqual(new IntervalEndpoints(IntervalEndpoint.Missing, new IntervalEndpoint(End)),
                               new IntervalEndpoints(IntervalEndpoint.Infinite, new IntervalEndpoint(End)));
        }

        // ----------------------------------------------------------------------
        // Small temp-folder fixture: dump the four objects and read them back,
        // proving the tri-state survives an external exchange.
        // ----------------------------------------------------------------------

        [Test]
        public void TempFolderFixture_FourObjectsRoundTrip()
        {
            var finite = new IntervalEndpoints(new IntervalEndpoint(Start), new IntervalEndpoint(End));
            var infinite = new IntervalEndpoints(IntervalEndpoint.Infinite, IntervalEndpoint.Infinite);
            var missing = new IntervalEndpoints(IntervalEndpoint.Missing, IntervalEndpoint.Missing);
            var reversed = new IntervalEndpoints(new IntervalEndpoint(End), new IntervalEndpoint(Start));
            string[] expectedText =
            {
                $"{Iso(Start)}/{Iso(End)}",
                "StartOfTime/EndOfTime",
                "/",
                $"{Iso(End)}/{Iso(Start)}",
            };
            var objects = new[] { finite, infinite, missing, reversed };

            string directory = Path.Combine(Path.GetTempPath(), "NodaTime-IntervalPatternTest-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                var report = new StringBuilder();
                for (int i = 0; i < objects.Length; i++)
                {
                    string file = Path.Combine(directory, "interval-" + i + ".txt");
                    File.WriteAllText(file, IntervalPattern.ExtendedIso.Format(objects[i]));
                    string readBack = File.ReadAllText(file);
                    Assert.AreEqual(expectedText[i], readBack);

                    var reparsed = IntervalPattern.ExtendedIso.ParseEndpoints(readBack).GetValueOrThrow();
                    Assert.AreEqual(objects[i], reparsed, "object " + i + " tri-state round-trip");

                    // finite and infinite are resolvable on the default interface; missing is rejected;
                    // reversed is rejected at the end endpoint.
                    var intervalResult = IntervalPattern.ExtendedIso.Parse(readBack);
                    report.AppendLine(Path.GetFileName(file) + ": '" + readBack + "' => " +
                        "start=" + reparsed.Start.State + ",end=" + reparsed.End.State +
                        ",intervalParseSuccess=" + intervalResult.Success);
                }
                string reportPath = Path.Combine(directory, "report.txt");
                File.WriteAllText(reportPath, report.ToString());
                TestContext.WriteLine("Temp-folder fixture: " + directory);
                TestContext.WriteLine(report.ToString());
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}
