// Copyright 2026 The Noda Time Authors. All rights reserved.
// Use of this source code is governed by the Apache License 2.0,
// as found in the LICENSE.txt file.

using System;

namespace NodaTime.Text
{
    /// <summary>
    /// Internal signal raised by <see cref="MissingEndpointPolicy.Reject"/> when a missing endpoint cannot be
    /// resolved. <see cref="IntervalPattern"/> converts it into a positioned <see cref="UnparsableValueException"/>.
    /// </summary>
    internal sealed class MissingEndpointException : Exception
    {
        internal MissingEndpointException(bool isStart)
        {
            IsStart = isStart;
        }

        /// <summary>true if the missing endpoint is the start; false if it is the end.</summary>
        internal bool IsStart { get; }

        internal UnparsableValueException ToException(string text) =>
            new UnparsableValueException(
                IsStart ? TextErrorMessages.MissingStartEndpoint : TextErrorMessages.MissingEndEndpoint,
                text, -1);
    }
}
