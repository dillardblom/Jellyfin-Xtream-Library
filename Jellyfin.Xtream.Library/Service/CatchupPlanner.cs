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
using System.Collections.Generic;
using System.Linq;
using Jellyfin.Xtream.Library.Client.Models;

namespace Jellyfin.Xtream.Library.Service;

/// <summary>
/// Decides what the catch-up browser shows: which channels, how many days back, and which
/// programmes fall on a given day (GitHub #108).
/// <para>
/// Every method here is pure. The IChannel implementation that calls them is Jellyfin
/// plumbing and cannot be unit tested without a server, so anything worth asserting lives here.
/// </para>
/// </summary>
public static class CatchupPlanner
{
    /// <summary>
    /// The channels a user can catch up on.
    /// <para>
    /// Same predicate the M3U already uses to decide which channels carry catch-up attributes
    /// (<see cref="LiveTvService.GenerateM3U"/>), so the two cannot disagree about what the
    /// provider offers.
    /// </para>
    /// </summary>
    /// <param name="channels">Channels to filter. May be null.</param>
    /// <returns>Channels the provider keeps an archive for, in the order given.</returns>
    public static IReadOnlyList<LiveStreamInfo> CatchupChannels(IEnumerable<LiveStreamInfo>? channels)
        => channels?.Where(c => c is { TvArchive: true, TvArchiveDuration: > 0 }).ToList()
           ?? new List<LiveStreamInfo>();

    /// <summary>
    /// How many day folders to show for a channel.
    /// <para>
    /// The lower of what the user asked for and what the provider actually keeps. Offering a day
    /// the provider has already dropped produces a folder of programmes that all fail to play.
    /// </para>
    /// </summary>
    /// <param name="configuredCatchupDays">The <c>CatchupDays</c> setting.</param>
    /// <param name="tvArchiveDuration">Days of archive the provider reports for this channel.</param>
    /// <returns>The number of day folders, never negative.</returns>
    public static int DayCount(int configuredCatchupDays, int tvArchiveDuration)
        => Math.Max(0, Math.Min(configuredCatchupDays, tvArchiveDuration));

    /// <summary>
    /// The UTC instants bounding one calendar day, as the server's own clock sees that day.
    /// <para>
    /// A day folder has to mean the day a viewer had, not a UTC day, otherwise the evening's
    /// programmes land under tomorrow for anyone east of Greenwich. The boundary is therefore
    /// computed in the given zone and converted back, which also gets the 23 and 25 hour days at a
    /// DST switch right, since the conversion accounts for the offset on each side.
    /// </para>
    /// </summary>
    /// <param name="daysAgo">Days before today. Zero is today.</param>
    /// <param name="zone">The zone whose calendar day is meant, normally the server's.</param>
    /// <param name="nowUtc">The current instant.</param>
    /// <returns>Start inclusive, end exclusive.</returns>
    public static (DateTimeOffset FromUtc, DateTimeOffset ToUtc) DayWindowUtc(int daysAgo, TimeZoneInfo zone, DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(zone);

        var localNow = TimeZoneInfo.ConvertTime(nowUtc, zone);
        var localMidnight = localNow.Date.AddDays(-Math.Max(0, daysAgo));

        var from = ToUtcInstant(localMidnight, zone);
        var to = ToUtcInstant(localMidnight.AddDays(1), zone);
        return (from, to);
    }

    /// <summary>
    /// The programmes that belong in one day folder.
    /// <para>
    /// Overlap, not containment: a programme running from 23:30 to 00:30 is a real thing to want to
    /// watch and appears under both days rather than falling between them.
    /// </para>
    /// <para>
    /// Two exclusions. A programme that has not finished is not catch-up, it is live, and Jellyfin
    /// already plays that. A programme older than the archive horizon is gone from the provider,
    /// and listing it only produces an item that fails when clicked.
    /// </para>
    /// </summary>
    /// <param name="all">Programmes for one channel. May be null.</param>
    /// <param name="fromUtc">Window start, inclusive.</param>
    /// <param name="toUtc">Window end, exclusive.</param>
    /// <param name="nowUtc">The current instant.</param>
    /// <param name="archiveHorizonUtc">Oldest instant the provider still serves.</param>
    /// <returns>Matching programmes, oldest first.</returns>
    public static IReadOnlyList<EpgProgram> ProgrammesInWindow(
        IEnumerable<EpgProgram>? all,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        DateTimeOffset nowUtc,
        DateTimeOffset archiveHorizonUtc)
    {
        if (all == null)
        {
            return new List<EpgProgram>();
        }

        long from = fromUtc.ToUnixTimeSeconds();
        long to = toUtc.ToUnixTimeSeconds();
        long now = nowUtc.ToUnixTimeSeconds();
        long horizon = archiveHorizonUtc.ToUnixTimeSeconds();

        return all
            .Where(p => p.StopTimestamp > p.StartTimestamp)
            .Where(p => p.StartTimestamp < to && p.StopTimestamp > from)
            .Where(p => p.StopTimestamp <= now)
            .Where(p => p.StartTimestamp >= horizon)
            .OrderBy(p => p.StartTimestamp)
            .ToList();
    }

    /// <summary>
    /// The oldest instant a channel's archive still covers.
    /// </summary>
    /// <param name="dayCount">Result of <see cref="DayCount"/>.</param>
    /// <param name="zone">The zone whose calendar day is meant.</param>
    /// <param name="nowUtc">The current instant.</param>
    /// <returns>Midnight local time, <paramref name="dayCount"/> days back, as a UTC instant.</returns>
    public static DateTimeOffset ArchiveHorizonUtc(int dayCount, TimeZoneInfo zone, DateTimeOffset nowUtc)
        => DayWindowUtc(Math.Max(0, dayCount), zone, nowUtc).FromUtc;

    /// <summary>
    /// Programme duration in whole minutes, which is the unit the Xtream timeshift endpoint takes.
    /// <para>
    /// Rounded up, never below one. A programme listed as shorter than a minute is a bad EPG entry
    /// rather than a real thing, and asking a provider for a zero minute recording returns nothing.
    /// </para>
    /// </summary>
    /// <param name="startUnix">Start, UTC unix seconds.</param>
    /// <param name="stopUnix">Stop, UTC unix seconds.</param>
    /// <returns>Duration in minutes, at least one.</returns>
    public static int DurationMinutes(long startUnix, long stopUnix)
    {
        long seconds = stopUnix - startUnix;
        if (seconds <= 0)
        {
            return 1;
        }

        return (int)Math.Max(1, (seconds + 59) / 60);
    }

    private static DateTimeOffset ToUtcInstant(DateTime unspecifiedLocal, TimeZoneInfo zone)
    {
        var asUnspecified = DateTime.SpecifyKind(unspecifiedLocal, DateTimeKind.Unspecified);

        // A local midnight can be skipped entirely by a DST jump. Walking forward to the first
        // time that does exist keeps the window contiguous with the previous day rather than
        // throwing on the one night a year it happens.
        while (zone.IsInvalidTime(asUnspecified))
        {
            asUnspecified = asUnspecified.AddMinutes(1);
        }

        return new DateTimeOffset(asUnspecified, zone.GetUtcOffset(asUnspecified));
    }
}
