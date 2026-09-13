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
/// Works out which clock a provider means when it reads a timeshift <c>start</c> (GitHub #108).
/// <para>
/// This is the one thing in catch-up that fails silently. Every other mistake produces an error;
/// getting the clock wrong returns a healthy stream of the wrong programme, an hour or two off,
/// with nothing in any log to say so. Hence a derivation that prefers what the provider says about
/// itself, a recorded source so a support answer is one log line, and a manual override for when
/// both derivations are wrong.
/// </para>
/// </summary>
public static class CatchupClock
{
    /// <summary>Largest offset any real timezone has, used to reject nonsense.</summary>
    private static readonly TimeSpan MaxOffset = TimeSpan.FromHours(14);

    /// <summary>
    /// Derives the provider's offset by comparing the two clocks it reports for the same instant.
    /// <para>
    /// <c>player_api.php</c> returns <c>timestamp_now</c> (UTC) and <c>time_now</c> (the panel's
    /// own local rendering). The difference is the panel's offset, stated by the panel itself,
    /// which beats trusting a timezone name it also reports.
    /// </para>
    /// </summary>
    /// <param name="serverTimeUtc">The provider's <c>timestamp_now</c>.</param>
    /// <param name="serverTimeLocalRaw">The provider's <c>time_now</c>.</param>
    /// <param name="offset">The derived offset, rounded to a quarter hour.</param>
    /// <returns>True when both values were usable.</returns>
    public static bool TryDeriveOffset(DateTime serverTimeUtc, string? serverTimeLocalRaw, out TimeSpan offset)
    {
        offset = TimeSpan.Zero;
        if (string.IsNullOrWhiteSpace(serverTimeLocalRaw) || serverTimeUtc == default)
        {
            return false;
        }

        if (!DateTime.TryParse(
                serverTimeLocalRaw,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.NoCurrentDateDefault,
                out var local))
        {
            return false;
        }

        var raw = DateTime.SpecifyKind(local, DateTimeKind.Utc) - DateTime.SpecifyKind(serverTimeUtc, DateTimeKind.Utc);
        if (raw > MaxOffset || raw < -MaxOffset)
        {
            return false;
        }

        offset = RoundToQuarterHour(raw);
        return true;
    }

    /// <summary>
    /// Rounds to the nearest quarter hour. Real zones are whole quarter hours apart; anything else
    /// in the measurement is the second or two of latency between the two fields being rendered.
    /// </summary>
    /// <param name="value">The measured difference.</param>
    /// <returns>The rounded offset.</returns>
    public static TimeSpan RoundToQuarterHour(TimeSpan value)
    {
        const long QuarterTicks = TimeSpan.TicksPerMinute * 15;
        var rounded = (long)Math.Round((double)value.Ticks / QuarterTicks, MidpointRounding.AwayFromZero) * QuarterTicks;
        return TimeSpan.FromTicks(rounded);
    }

    /// <summary>
    /// Resolves the offset to express a timeshift <c>start</c> in.
    /// </summary>
    /// <param name="serverTimeUtc">The provider's <c>timestamp_now</c>, if known.</param>
    /// <param name="serverTimeLocalRaw">The provider's <c>time_now</c>, if known.</param>
    /// <param name="ianaTimeZoneId">The provider's reported timezone name, if any.</param>
    /// <param name="atUtc">The instant the offset is wanted for, so a programme from before a
    /// daylight-saving change is not shifted by today's offset.</param>
    /// <param name="manualShiftMinutes">User correction, added last.</param>
    /// <returns>The offset and where it came from.</returns>
    public static ProviderClock Resolve(
        DateTime serverTimeUtc,
        string? serverTimeLocalRaw,
        string? ianaTimeZoneId,
        DateTimeOffset atUtc,
        int manualShiftMinutes)
    {
        var shift = TimeSpan.FromMinutes(Math.Clamp(manualShiftMinutes, -720, 720));

        if (TryDeriveOffset(serverTimeUtc, serverTimeLocalRaw, out var measured))
        {
            return new ProviderClock(measured + shift, "reported by provider");
        }

        if (!string.IsNullOrWhiteSpace(ianaTimeZoneId))
        {
            try
            {
                var zone = TimeZoneInfo.FindSystemTimeZoneById(ianaTimeZoneId);
                return new ProviderClock(zone.GetUtcOffset(atUtc) + shift, "provider timezone name");
            }
            catch (TimeZoneNotFoundException)
            {
                // The panel can report a name this host has never heard of.
            }
            catch (InvalidTimeZoneException)
            {
                // Or one whose rules are corrupt.
            }
        }

        return new ProviderClock(shift, "assumed UTC");
    }
}
