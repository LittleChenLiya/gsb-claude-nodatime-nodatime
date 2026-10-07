// Copyright 2026 The Noda Time Authors. All rights reserved.
// Use of this source code is governed by the Apache License 2.0,
// as found in the LICENSE.txt file.

using NodaTime.Annotations;
using NodaTime.Utility;
using System.Text;

namespace NodaTime.Text
{
    /// <summary>
    /// Strict "local date/time to a specified offset" parse combinator.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This factory combines a <see cref="LocalDateTimePattern"/> (which provides the date/time
    /// shape, the culture and all parse error positions) with an
    /// <see cref="OffsetPattern"/> and an explicitly specified offset.
    /// </para>
    /// <para>
    /// When the input text contains an offset, it must be equal to the specified offset;
    /// a conflicting offset declaration causes the parse to fail. When the text
    /// contains no offset, the specified offset is assigned to the parsed local date/time.
    /// Any trailing text (including a second, duplicate offset declaration) is rejected.
    /// No time zone is involved at any point: time zone names are never interpreted as
    /// offsets, and the system local time zone is never used implicitly.
    /// </para>
    /// </remarks>
    internal static class StrictOffsetDateTimePattern
    {
        /// <summary>
        /// Creates a strict pattern that parses a local date/time followed by an optional offset,
        /// requiring any declared offset to match <paramref name="expectedOffset"/>.
        /// </summary>
        /// <param name="localPattern">The pattern used for the local date/time part; its culture is retained.</param>
        /// <param name="offsetPattern">The partial pattern used to parse an offset declared in the text.</param>
        /// <param name="expectedOffset">The offset the result must have; it is also assigned when the text declares none.</param>
        /// <returns>A pattern producing an <see cref="OffsetDateTime"/> with the specified offset.</returns>
        internal static IPattern<OffsetDateTime> Create(
            LocalDateTimePattern localPattern, OffsetPattern offsetPattern, Offset expectedOffset)
        {
            Preconditions.CheckNotNull(localPattern, nameof(localPattern));
            Preconditions.CheckNotNull(offsetPattern, nameof(offsetPattern));
            return new StrictPattern(localPattern.UnderlyingPattern, offsetPattern.UnderlyingPattern, expectedOffset);
        }

        private sealed class StrictPattern : IPattern<OffsetDateTime>
        {
            private readonly IPartialPattern<LocalDateTime> localPattern;
            private readonly IPartialPattern<Offset> offsetPattern;
            private readonly Offset expectedOffset;

            internal StrictPattern(
                IPartialPattern<LocalDateTime> localPattern,
                IPartialPattern<Offset> offsetPattern,
                Offset expectedOffset)
            {
                this.localPattern = localPattern;
                this.offsetPattern = offsetPattern;
                this.expectedOffset = expectedOffset;
            }

            public ParseResult<OffsetDateTime> Parse([SpecialNullHandling] string text)
            {
                if (text is null)
                {
                    return ParseResult<OffsetDateTime>.ArgumentNull(nameof(text));
                }
                if (text.Length == 0)
                {
                    return ParseResult<OffsetDateTime>.ValueStringEmpty;
                }

                var cursor = new ValueCursor(text);
                // Prime the pump... the value cursor ends up *before* the first character, but
                // the parse steps always assume it is *on* the right character.
                cursor.MoveNext();

                // The local date/time pattern owns all the date/time error positions and culture.
                var localResult = localPattern.ParsePartial(cursor);
                if (!localResult.Success)
                {
                    return localResult.ConvertError<OffsetDateTime>();
                }

                // An offset declared in the text is only accepted when it equals the
                // explicitly specified offset; a conflicting declaration is rejected.
                if (cursor.Current != TextCursor.Nul)
                {
                    int offsetStartIndex = cursor.Index;
                    var offsetResult = offsetPattern.ParsePartial(cursor);
                    if (!offsetResult.Success)
                    {
                        return offsetResult.ConvertError<OffsetDateTime>();
                    }
                    if (offsetResult.Value != expectedOffset)
                    {
                        // Report the failure at the start of the declared offset.
                        cursor.Move(offsetStartIndex);
                        return ParseResult<OffsetDateTime>.ConflictingOffset(cursor, offsetResult.Value, expectedOffset);
                    }
                }

                // Trailing text is rejected. In particular this means a *second* (duplicate)
                // offset declaration can never be silently accepted.
                if (cursor.Current != TextCursor.Nul)
                {
                    return ParseResult<OffsetDateTime>.ExtraValueCharacters(cursor, cursor.Remainder);
                }

                // No time zone lookup happens here: the local date/time is simply stamped
                // with the explicitly specified offset.
                return localResult.Convert(local => local.WithOffset(expectedOffset));
            }

            public string Format(OffsetDateTime value)
            {
                var builder = new StringBuilder();
                AppendFormat(value, builder);
                return builder.ToString();
            }

            public StringBuilder AppendFormat(OffsetDateTime value, StringBuilder builder)
            {
                Preconditions.CheckNotNull(builder, nameof(builder));
                localPattern.AppendFormat(value.LocalDateTime, builder);
                if (value.Offset == expectedOffset)
                {
                    offsetPattern.AppendFormat(value.Offset, builder);
                }
                return builder;
            }
        }
    }
}
