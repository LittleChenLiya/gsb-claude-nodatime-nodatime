// Copyright 2026 The Noda Time Authors. All rights reserved.
// Use of this source code is governed by the Apache License 2.0,
// as found in the LICENSE.txt file.

using NodaTime.Annotations;
using NodaTime.Utility;
using System;
using System.Globalization;
using System.Text;
using static System.FormattableString;

namespace NodaTime.Text
{
    /// <summary>
    /// Represents a pattern for parsing and formatting <see cref="Interval"/> values as "start/end" text, with
    /// explicit support for the three states each endpoint can be in while being exchanged: a concrete finite
    /// <see cref="Instant"/>, an explicitly infinite endpoint ("StartOfTime"/"EndOfTime"), or a missing endpoint
    /// (an empty segment).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The endpoint syntax is a slash-separated pair: <c>start/end</c>. Each side is either an extended
    /// ISO-8601 instant, <c>StartOfTime</c> (start side) / <c>EndOfTime</c> (end side) for an explicitly
    /// infinite endpoint, or empty to indicate that the endpoint is missing. The tokens match those emitted by
    /// <see cref="Interval.ToString"/>, so an interval formatted with <see cref="ExtendedIso"/> round-trips via
    /// <see cref="Parse(string)"/>.
    /// </para>
    /// <para>
    /// A missing endpoint is distinct from an infinite one and is <i>never</i> replaced with the current time
    /// automatically. It is resolved according to the <see cref="MissingEndpointPolicy"/> supplied when the
    /// pattern was created; the default pattern uses <see cref="Text.MissingEndpointPolicy.Reject"/> for both
    /// ends. Call <see cref="ParseEndpoints(string)"/> to obtain the unresolved three-state endpoints directly.
    /// </para>
    /// <para>
    /// Parse failures are returned (not thrown) as <see cref="ParseResult{T}"/>. A reversed interval (end before
    /// start) is rejected with the error positioned at the start of the end endpoint in the original text.
    /// </para>
    /// </remarks>
    /// <threadsafety>
    /// This type is immutable and thread-safe (assuming any custom <see cref="MissingEndpointPolicy"/> is itself
    /// thread-safe).
    /// </threadsafety>
    [Immutable]
    public sealed class IntervalPattern : IPattern<Interval>
    {
        /// <summary>
        /// Gets an invariant interval pattern using extended ISO-8601 instants, the "StartOfTime"/"EndOfTime"
        /// tokens for infinite endpoints, and <see cref="Text.MissingEndpointPolicy.Reject"/> for missing
        /// endpoints.
        /// </summary>
        /// <value>The extended ISO interval pattern.</value>
        public static IntervalPattern ExtendedIso { get; } =
            new IntervalPattern(InstantPattern.ExtendedIso, MissingEndpointPolicy.Reject, MissingEndpointPolicy.Reject);

        private readonly IPattern<Instant> instantPattern;
        private readonly MissingEndpointPolicy startPolicy;
        private readonly MissingEndpointPolicy endPolicy;

        private IntervalPattern(IPattern<Instant> instantPattern, MissingEndpointPolicy startPolicy, MissingEndpointPolicy endPolicy)
        {
            this.instantPattern = instantPattern;
            this.startPolicy = startPolicy;
            this.endPolicy = endPolicy;
        }

        /// <summary>
        /// Creates an interval pattern which uses <see cref="InstantPattern.ExtendedIso"/> for concrete instants
        /// and the given policies to resolve missing endpoints.
        /// </summary>
        /// <param name="missingStartPolicy">The policy used when the start endpoint is missing.</param>
        /// <param name="missingEndPolicy">The policy used when the end endpoint is missing.</param>
        /// <returns>The new pattern.</returns>
        public static IntervalPattern Create(MissingEndpointPolicy missingStartPolicy, MissingEndpointPolicy missingEndPolicy)
        {
            Preconditions.CheckNotNull(missingStartPolicy, nameof(missingStartPolicy));
            Preconditions.CheckNotNull(missingEndPolicy, nameof(missingEndPolicy));
            return new IntervalPattern(InstantPattern.ExtendedIso, missingStartPolicy, missingEndPolicy);
        }

        /// <summary>
        /// Creates an interval pattern which uses <see cref="InstantPattern.ExtendedIso"/> for concrete instants
        /// and the same policy to resolve either missing endpoint.
        /// </summary>
        /// <param name="missingEndpointPolicy">The policy used when either endpoint is missing.</param>
        /// <returns>The new pattern.</returns>
        public static IntervalPattern Create(MissingEndpointPolicy missingEndpointPolicy) =>
            Create(missingEndpointPolicy, missingEndpointPolicy);

        /// <summary>
        /// Parses the given text as "start/end", resolving any missing endpoints using this pattern's policies
        /// and producing an <see cref="Interval"/>.
        /// </summary>
        /// <remarks>
        /// This method never throws for malformed input (barring a bug in Noda Time itself): failures are
        /// reported via <see cref="ParseResult{T}"/>. No clock is consulted unless a caller-supplied
        /// <see cref="MissingEndpointPolicy"/> chooses to read one.
        /// </remarks>
        /// <param name="text">The text to parse.</param>
        /// <returns>The parse result.</returns>
        public ParseResult<Interval> Parse([SpecialNullHandling] string text)
        {
            var raw = ParseRaw(text);
            if (!raw.Success)
            {
                return raw.ConvertError<Interval>();
            }
            var value = raw.Value;

            Instant? start;
            Instant? end;
            try
            {
                start = Resolve(value.Start, startPolicy, isStart: true);
                end = Resolve(value.End, endPolicy, isStart: false);
            }
            catch (MissingEndpointException e)
            {
                return ParseResult<Interval>.ForException(() => e.ToException(text));
            }
            catch (UnparsableValueException e) when (e.Index < 0)
            {
                // A caller policy chose to reject with a position-less error. Attach the whole input.
                return ParseResult<Interval>.ForException(
                    () => new UnparsableValueException(e.Message, text, -1, e));
            }
            catch (Exception e)
            {
                // A caller-supplied resolver threw. Keep the IPattern contract: parsing reports, it never throws.
                return ParseResult<Interval>.ForException(
                    () => new UnparsableValueException(e.Message, text, -1, e));
            }

            if (end < start)
            {
                return FailureAt<Interval>(text, value.EndIndex, TextErrorMessages.IntervalEndBeforeStart);
            }
            return ParseResult<Interval>.ForValue(new Interval(start, end));
        }

        /// <summary>
        /// Parses the given text as "start/end" without resolving missing endpoints, returning the three-state
        /// endpoints (missing, infinite, or concrete instant) directly.
        /// </summary>
        /// <remarks>
        /// Order is not validated here: an end before the start is only rejected when constructing an
        /// <see cref="Interval"/> via
        /// <see cref="IntervalEndpoints.ToInterval(MissingEndpointPolicy, MissingEndpointPolicy)"/>.
        /// </remarks>
        /// <param name="text">The text to parse.</param>
        /// <returns>The parse result carrying the unresolved endpoints.</returns>
        public ParseResult<IntervalEndpoints> ParseEndpoints([SpecialNullHandling] string text)
        {
            var raw = ParseRaw(text);
            return raw.Convert(x => new IntervalEndpoints(x.Start, x.End));
        }

        /// <summary>
        /// Formats the given interval as "start/end" text. Finite endpoints use extended ISO-8601; infinite
        /// endpoints are rendered as "StartOfTime"/"EndOfTime". (An <see cref="Interval"/> cannot represent a
        /// missing endpoint.)
        /// </summary>
        /// <param name="value">The interval to format.</param>
        /// <returns>The formatted text.</returns>
        public string Format(Interval value)
        {
            value.Deconstruct(out Instant? start, out Instant? end);
            return Invariant($"{FormatInstant(start, isStart: true)}/{FormatInstant(end, isStart: false)}");
        }

        /// <summary>
        /// Formats the given three-state endpoints as "start/end" text. A missing endpoint renders as an empty
        /// segment, an infinite endpoint as "StartOfTime"/"EndOfTime", and a concrete endpoint in extended
        /// ISO-8601 form.
        /// </summary>
        /// <param name="value">The endpoints to format.</param>
        /// <returns>The formatted text.</returns>
        public string Format(IntervalEndpoints value)
        {
            string startText = value.Start.State switch
            {
                IntervalEndpointState.Missing => "",
                IntervalEndpointState.Infinite => InstantPatternParser.BeforeMinValueText,
                _ => instantPattern.Format(value.Start.Instant),
            };
            string endText = value.End.State switch
            {
                IntervalEndpointState.Missing => "",
                IntervalEndpointState.Infinite => InstantPatternParser.AfterMaxValueText,
                _ => instantPattern.Format(value.End.Instant),
            };
            return Invariant($"{startText}/{endText}");
        }

        /// <inheritdoc />
        StringBuilder IPattern<Interval>.AppendFormat(Interval value, StringBuilder builder)
        {
            Preconditions.CheckNotNull(builder, nameof(builder));
            return builder.Append(Format(value));
        }

        private string FormatInstant(Instant? value, bool isStart)
        {
            if (value is Instant instant)
            {
                return instantPattern.Format(instant);
            }
            return isStart ? InstantPatternParser.BeforeMinValueText : InstantPatternParser.AfterMaxValueText;
        }

        private static Instant? Resolve(IntervalEndpoint endpoint, MissingEndpointPolicy policy, bool isStart)
        {
            switch (endpoint.State)
            {
                case IntervalEndpointState.Infinite:
                    return null;
                case IntervalEndpointState.Instant:
                    return endpoint.Instant;
                default:
                    return policy.Resolve(isStart);
            }
        }

        // Internal raw parse result: endpoints plus the original index of the start of the end segment,
        // so that order/policy failures can be reported at the right place in the original text.
        private readonly struct ParsedInterval
        {
            internal ParsedInterval(IntervalEndpoint start, IntervalEndpoint end, int endIndex)
            {
                Start = start;
                End = end;
                EndIndex = endIndex;
            }

            internal IntervalEndpoint Start { get; }
            internal IntervalEndpoint End { get; }
            internal int EndIndex { get; }
        }

        private ParseResult<ParsedInterval> ParseRaw(string text)
        {
            if (text is null)
            {
                return ParseResult<ParsedInterval>.ArgumentNull(nameof(text));
            }
            if (text.Length == 0)
            {
                return ParseResult<ParsedInterval>.ValueStringEmpty;
            }

            int separator = text.IndexOf('/');
            if (separator < 0)
            {
                return FailureAt<ParsedInterval>(text, text.Length, TextErrorMessages.MissingSeparator);
            }
            int secondSeparator = text.IndexOf('/', separator + 1);
            if (secondSeparator >= 0)
            {
                return FailureAt<ParsedInterval>(text, secondSeparator, TextErrorMessages.RepeatedSeparator);
            }

            var start = ParseSegment(text, 0, separator, isStart: true);
            if (!start.Success)
            {
                return start.ConvertError<ParsedInterval>();
            }
            int endStart = separator + 1;
            var end = ParseSegment(text, endStart, text.Length - endStart, isStart: false);
            if (!end.Success)
            {
                return end.ConvertError<ParsedInterval>();
            }
            return ParseResult<ParsedInterval>.ForValue(new ParsedInterval(start.Value, end.Value, endStart));
        }

        private ParseResult<IntervalEndpoint> ParseSegment(string text, int offset, int length, bool isStart)
        {
            string token = isStart
                ? InstantPatternParser.BeforeMinValueText
                : InstantPatternParser.AfterMaxValueText;

            if (length == 0)
            {
                return ParseResult<IntervalEndpoint>.ForValue(IntervalEndpoint.Missing);
            }
            if (length == token.Length &&
                string.CompareOrdinal(text, offset, token, 0, token.Length) == 0)
            {
                return ParseResult<IntervalEndpoint>.ForValue(IntervalEndpoint.Infinite);
            }

            string segment = text.Substring(offset, length);
            var instantResult = instantPattern.Parse(segment);
            if (!instantResult.Success)
            {
                // Translate the segment-relative failure into one positioned within the whole input text,
                // preserving the original location of the offending text.
                return ParseResult<IntervalEndpoint>.ForException(() =>
                {
                    var inner = (UnparsableValueException) instantResult.Exception;
                    int index = inner.Index < 0 ? -1 : offset + inner.Index;
                    return new UnparsableValueException(inner.Message, text, index, inner);
                });
            }
            return ParseResult<IntervalEndpoint>.ForValue(new IntervalEndpoint(instantResult.Value));
        }

        private static ParseResult<T> FailureAt<T>(string text, int index, string detailMessage)
        {
            return ParseResult<T>.ForException(() =>
            {
                var cursor = new ValueCursor(text);
                cursor.Move(index);
                string overallMessage = string.Format(
                    CultureInfo.CurrentCulture,
                    TextErrorMessages.UnparsableValue,
                    detailMessage,
                    cursor);
                return new UnparsableValueException(overallMessage, text, index);
            });
        }
    }
}
