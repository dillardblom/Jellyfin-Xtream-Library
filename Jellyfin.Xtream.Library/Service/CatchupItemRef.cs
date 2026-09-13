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

namespace Jellyfin.Xtream.Library.Service;

/// <summary>
/// What a catch-up item id points at (GitHub #108).
/// </summary>
public enum CatchupItemKind
{
    /// <summary>Not one of ours, or malformed.</summary>
    None = 0,

    /// <summary>A channel folder. Opening it lists days.</summary>
    Channel = 1,

    /// <summary>A day folder. Opening it lists programmes.</summary>
    Day = 2,

    /// <summary>A single finished programme. Playable, and a leaf.</summary>
    Programme = 3,
}

/// <summary>
/// A decoded catch-up item id. Which fields carry meaning depends on <see cref="Kind"/>.
/// </summary>
/// <param name="Kind">What the id points at.</param>
/// <param name="ProviderIndex">Index into the configured providers.</param>
/// <param name="StreamId">Xtream live stream id of the channel.</param>
/// <param name="DaysAgo">Days before today, for <see cref="CatchupItemKind.Day"/>.</param>
/// <param name="StartUnix">Programme start, UTC unix seconds.</param>
/// <param name="StopUnix">Programme stop, UTC unix seconds.</param>
public readonly record struct CatchupItemRef(
    CatchupItemKind Kind,
    int ProviderIndex,
    int StreamId,
    int DaysAgo,
    long StartUnix,
    long StopUnix);
