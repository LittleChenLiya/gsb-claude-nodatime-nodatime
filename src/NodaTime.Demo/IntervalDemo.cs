// Copyright 2019 The Noda Time Authors. All rights reserved.
// Use of this source code is governed by the Apache License 2.0,
// as found in the LICENSE.txt file.

using NodaTime.Text;
using NUnit.Framework;

namespace NodaTime.Demo
{
    public class IntervalDemo
    {
        [Test]
        public void Construction_WithNonNullableParameters()
        {
            Instant start = Instant.FromUtc(2019, 1, 1, 15, 25, 48);
            Instant end = Instant.FromUtc(2019, 1, 1, 16, 25, 48);
            Interval interval = Snippet.For(new Interval(start, end));
            Assert.AreEqual(start, interval.Start);
            Assert.AreEqual(end, interval.End);
        }

        [Test]
        public void Construction_WithNullableParameters()
        {
            Instant end = Instant.FromUtc(2019, 1, 1, 16, 25, 48);
            Interval interval = Snippet.For(new Interval(null, end));
            Assert.False(interval.HasStart);
            Assert.True(interval.HasEnd);
            Assert.AreEqual(end, interval.End);
        }

        [Test]
        public void Contains()
        {
            Instant start = Instant.FromUtc(2019, 1, 1, 15, 25, 48);
            Instant end = Instant.FromUtc(2019, 1, 1, 16, 25, 48);
            Interval interval = new Interval(start, end);
            Assert.True(Snippet.For(interval.Contains(Instant.FromUtc(2019, 1, 1, 15, 50, 50))));
        }

        [Test]
        public void Deconstruct()
        {
            Instant? start = Instant.FromUtc(2019, 1, 2, 3, 10, 11);
            Instant? end = Instant.FromUtc(2020, 4, 5, 6, 12, 13);
            Interval interval = new Interval(start, end);

            Snippet.SilentForAction(() => interval.Deconstruct(out _, out _));
            interval.Deconstruct(out start, out end);
            Assert.AreEqual(Instant.FromUtc(2019, 1, 2, 3, 10, 11), start);
            Assert.AreEqual(Instant.FromUtc(2020, 4, 5, 6, 12, 13), end);
        }

        [Test]
        public void DurationDemo()
        {
            Instant start = Instant.FromUtc(2019, 1, 1, 3, 10, 20);
            Instant end = Instant.FromUtc(2019, 1, 1, 9, 10, 20);
            Interval interval = new Interval(start, end);
            var duration = Snippet.For(interval.Duration);
            Assert.AreEqual(Duration.FromHours(6), duration);
        }

        [Test]
        public void ToStringDemo()
        {
            Instant start = Instant.FromUtc(2019, 1, 2, 10, 11, 12);
            Instant end = Instant.FromUtc(2020, 2, 3, 12, 13, 14);
            Interval interval = new Interval(start, end);
            var stringRepresentation = Snippet.For(interval.ToString());
            Assert.AreEqual("2019-01-02T10:11:12Z/2020-02-03T12:13:14Z", stringRepresentation);
        }

        [Test]
        public void Pattern_MissingEndpointsUseAnExplicitPolicyAndNeverUseNow()
        {
            // The default pattern accepts concrete instants and explicit infinity, but rejects
            // missing endpoints rather than filling them in with "now".
            Assert.IsTrue(IntervalPattern.ExtendedIso.Parse("StartOfTime/EndOfTime").Success);
            Assert.IsFalse(IntervalPattern.ExtendedIso.Parse("/").Success);

            // Missing endpoints are resolved only with a caller-supplied policy, e.g. "treat missing as infinite".
            var pattern = IntervalPattern.Create(MissingEndpointPolicy.TreatAsInfinite);
            Interval interval = Snippet.For(pattern.Parse("/").GetValueOrThrow());
            Assert.IsFalse(interval.HasStart);
            Assert.IsFalse(interval.HasEnd);
        }

        [Test]
        public void Pattern_ThreeStateEndpointsRoundTrip()
        {
            // ParseEndpoints keeps the missing/infinite/instant tri-state distinct.
            var endpoints = IntervalPattern.ExtendedIso.ParseEndpoints("/").GetValueOrThrow();
            Assert.AreEqual(IntervalEndpointState.Missing, endpoints.Start.State);
            Assert.AreEqual(IntervalEndpointState.Missing, endpoints.End.State);
            Assert.AreEqual("/", IntervalPattern.ExtendedIso.Format(endpoints));
        }
    }
}
