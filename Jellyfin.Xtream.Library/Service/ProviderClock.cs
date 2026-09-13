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
/// A provider's clock offset and how it was arrived at (GitHub #108).
/// </summary>
/// <param name="Offset">Offset from UTC to express a timeshift start in.</param>
/// <param name="Source">Where the offset came from, for the log line that answers "catch-up plays
/// the wrong programme".</param>
public readonly record struct ProviderClock(TimeSpan Offset, string Source);
