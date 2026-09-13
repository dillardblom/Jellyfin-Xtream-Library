// Copyright (C) 2024  Roland Breitschaft

// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

// GitHub #108. Jellyfin hands a folder id straight back to us with no context, so the id has to
// carry everything needed to answer the browse and, for a programme, to build the stream URL.

using FluentAssertions;
using Jellyfin.Xtream.Library.Service;
using Xunit;

namespace Jellyfin.Xtream.Library.Tests.Service;

public class CatchupItemIdTests
{
    [Fact]
    public void ChannelId_RoundTrips()
    {
        CatchupItemId.TryParse(CatchupItemId.ForChannel(2, 4711), out var parsed).Should().BeTrue();
        parsed.Kind.Should().Be(CatchupItemKind.Channel);
        parsed.ProviderIndex.Should().Be(2);
        parsed.StreamId.Should().Be(4711);
    }

    [Fact]
    public void DayId_RoundTrips()
    {
        CatchupItemId.TryParse(CatchupItemId.ForDay(0, 12, 3), out var parsed).Should().BeTrue();
        parsed.Kind.Should().Be(CatchupItemKind.Day);
        parsed.StreamId.Should().Be(12);
        parsed.DaysAgo.Should().Be(3);
    }

    [Fact]
    public void ProgrammeId_RoundTripsItsOwnTimeWindow()
    {
        // The window travels in the id so playback never depends on a later EPG fetch returning
        // the same programmes in the same order.
        CatchupItemId.TryParse(CatchupItemId.ForProgramme(1, 99, 1_700_000_000, 1_700_003_600), out var parsed)
            .Should().BeTrue();
        parsed.Kind.Should().Be(CatchupItemKind.Programme);
        parsed.ProviderIndex.Should().Be(1);
        parsed.StreamId.Should().Be(99);
        parsed.StartUnix.Should().Be(1_700_000_000);
        parsed.StopUnix.Should().Be(1_700_003_600);
    }

    [Fact]
    public void TheThreeKindsAreNotConfusedForEachOther()
    {
        CatchupItemId.TryParse(CatchupItemId.ForChannel(0, 5), out var channel).Should().BeTrue();
        CatchupItemId.TryParse(CatchupItemId.ForDay(0, 5, 0), out var day).Should().BeTrue();
        CatchupItemId.TryParse(CatchupItemId.ForProgramme(0, 5, 10, 20), out var programme).Should().BeTrue();

        channel.Kind.Should().Be(CatchupItemKind.Channel);
        day.Kind.Should().Be(CatchupItemKind.Day);
        programme.Kind.Should().Be(CatchupItemKind.Programme);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-ours")]
    [InlineData("xcu")]
    [InlineData("xcu|c")]
    [InlineData("xcu|z|0|5")]                 // unknown kind
    [InlineData("xcu|c|0|5|extra")]           // too many parts for a channel
    [InlineData("xcu|d|0|5")]                 // day missing its day number
    [InlineData("xcu|c|nope|5")]              // provider index not a number
    [InlineData("xcu|c|-1|5")]                // negative provider index
    [InlineData("xcu|d|0|5|-2")]              // negative days ago
    [InlineData("xcu|p|0|5|100")]             // programme missing its stop
    public void AnythingMalformedOrForeignIsRefusedRatherThanThrowing(string? id)
    {
        // Jellyfin can hand back an id written by an older build of the plugin. A browse has to
        // degrade to an empty folder, not to an exception.
        CatchupItemId.TryParse(id, out var parsed).Should().BeFalse();
        parsed.Kind.Should().Be(CatchupItemKind.None);
    }

    [Theory]
    [InlineData(100, 100)]
    [InlineData(100, 50)]
    public void AProgrammeThatDoesNotEndAfterItStartsIsRefused(long start, long stop)
    {
        // It would yield no duration, and a zero-length timeshift request is not worth sending.
        CatchupItemId.TryParse(CatchupItemId.ForProgramme(0, 5, start, stop), out _).Should().BeFalse();
    }
}
