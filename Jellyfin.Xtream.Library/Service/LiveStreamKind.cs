// Copyright (C) 2024  Roland Breitschaft

// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.

// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <https://www.gnu.org/licenses/>.

using System;

namespace Jellyfin.Xtream.Library.Service;

/// <summary>
/// Tells radio channels apart from television, from the <c>stream_type</c> the provider reports
/// (GitHub #112).
/// <para>
/// Deliberately narrow. Only the one value that is known to mean radio counts, because
/// <c>stream_type</c> is also where providers put their own bookkeeping: one was found using
/// <c>created_live</c> across Kids, Movies and Sports, nothing to do with radio. Treating anything
/// unrecognised as radio would file real television under the wrong heading, and a viewer who
/// cannot find a channel at all is worse off than one whose radio sits in the TV list.
/// </para>
/// </summary>
public static class LiveStreamKind
{
    /// <summary>
    /// The single <c>stream_type</c> value that means radio. Confirmed against a real provider
    /// rather than inferred.
    /// </summary>
    public const string RadioStreamType = "radio_streams";

    /// <summary>
    /// Whether a channel is radio rather than television.
    /// </summary>
    /// <param name="streamType">The provider's <c>stream_type</c>, which may be null or empty.</param>
    /// <returns>True only for a value the provider is known to use for radio.</returns>
    public static bool IsRadio(string? streamType)
        => !string.IsNullOrWhiteSpace(streamType)
           && streamType.Trim().Equals(RadioStreamType, StringComparison.OrdinalIgnoreCase);
}
