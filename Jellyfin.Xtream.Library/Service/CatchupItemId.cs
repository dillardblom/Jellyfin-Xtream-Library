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
using System.Globalization;

namespace Jellyfin.Xtream.Library.Service;

/// <summary>
/// Encodes and decodes the ids Jellyfin hands back to us as <c>InternalChannelItemQuery.FolderId</c>
/// (GitHub #108).
/// <para>
/// Readable strings rather than the four-int32s-packed-into-a-GUID scheme the Kevinjil plugin uses.
/// That scheme gives slot 1 two different meanings, category id on a folder and stream id on a
/// media item, which is only safe because its media ids are never parsed back. A prefixed string
/// costs nothing, cannot collide, and says what it is in a log line.
/// </para>
/// <para>
/// A programme id carries its own time window rather than a position in some list, so playback
/// never depends on a later EPG fetch returning the same programmes in the same order.
/// </para>
/// </summary>
public static class CatchupItemId
{
    private const string Prefix = "xcu";
    private const char Separator = '|';

    /// <summary>
    /// Builds the id of a channel folder.
    /// </summary>
    /// <param name="providerIndex">Index into the configured providers.</param>
    /// <param name="streamId">Xtream live stream id.</param>
    /// <returns>The encoded id.</returns>
    public static string ForChannel(int providerIndex, int streamId)
        => string.Create(CultureInfo.InvariantCulture, $"{Prefix}|c|{providerIndex}|{streamId}");

    /// <summary>
    /// Builds the id of a day folder.
    /// </summary>
    /// <param name="providerIndex">Index into the configured providers.</param>
    /// <param name="streamId">Xtream live stream id.</param>
    /// <param name="daysAgo">Days before today. Zero is today.</param>
    /// <returns>The encoded id.</returns>
    public static string ForDay(int providerIndex, int streamId, int daysAgo)
        => string.Create(CultureInfo.InvariantCulture, $"{Prefix}|d|{providerIndex}|{streamId}|{daysAgo}");

    /// <summary>
    /// Builds the id of a single programme.
    /// </summary>
    /// <param name="providerIndex">Index into the configured providers.</param>
    /// <param name="streamId">Xtream live stream id.</param>
    /// <param name="startUnix">Programme start, UTC unix seconds.</param>
    /// <param name="stopUnix">Programme stop, UTC unix seconds.</param>
    /// <returns>The encoded id.</returns>
    public static string ForProgramme(int providerIndex, int streamId, long startUnix, long stopUnix)
        => string.Create(CultureInfo.InvariantCulture, $"{Prefix}|p|{providerIndex}|{streamId}|{startUnix}|{stopUnix}");

    /// <summary>
    /// Decodes an id produced by this class.
    /// <para>
    /// Anything unrecognised returns false rather than throwing: Jellyfin can hand back an id
    /// written by an older version of the plugin, and a browse must degrade to an empty folder
    /// rather than to an error.
    /// </para>
    /// </summary>
    /// <param name="id">The id to decode.</param>
    /// <param name="parsed">The decoded reference, or default when this returns false.</param>
    /// <returns>True when the id was one of ours and well formed.</returns>
    public static bool TryParse(string? id, out CatchupItemRef parsed)
    {
        parsed = default;
        if (string.IsNullOrEmpty(id))
        {
            return false;
        }

        var parts = id.Split(Separator);
        if (parts.Length < 4 || !string.Equals(parts[0], Prefix, StringComparison.Ordinal))
        {
            return false;
        }

        if (!TryParseInt(parts[2], out int providerIndex) || providerIndex < 0)
        {
            return false;
        }

        if (!TryParseInt(parts[3], out int streamId) || streamId < 0)
        {
            return false;
        }

        switch (parts[1])
        {
            case "c" when parts.Length == 4:
                parsed = new CatchupItemRef(CatchupItemKind.Channel, providerIndex, streamId, 0, 0, 0);
                return true;

            case "d" when parts.Length == 5:
                if (!TryParseInt(parts[4], out int daysAgo) || daysAgo < 0)
                {
                    return false;
                }

                parsed = new CatchupItemRef(CatchupItemKind.Day, providerIndex, streamId, daysAgo, 0, 0);
                return true;

            case "p" when parts.Length == 6:
                if (!TryParseLong(parts[4], out long startUnix) || !TryParseLong(parts[5], out long stopUnix))
                {
                    return false;
                }

                // A programme that does not end after it starts cannot produce a duration, and a
                // zero-length timeshift request is not something to hand to a provider.
                if (stopUnix <= startUnix)
                {
                    return false;
                }

                parsed = new CatchupItemRef(CatchupItemKind.Programme, providerIndex, streamId, 0, startUnix, stopUnix);
                return true;

            default:
                return false;
        }
    }

    private static bool TryParseInt(string value, out int result)
        => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out result);

    private static bool TryParseLong(string value, out long result)
        => long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out result);
}
